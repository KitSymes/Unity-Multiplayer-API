using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KitSymes.GTRP.SourceGenerators
{
    [Generator]
    public class NetworkBehaviourSyncGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new NetworkBehaviourSyncSyntaxContextReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            NetworkBehaviourSyncSyntaxContextReceiver syntaxReceiver = (NetworkBehaviourSyncSyntaxContextReceiver)context.SyntaxContextReceiver;

            INamedTypeSymbol attributeSymbol = context.Compilation.GetTypeByMetadataName("KitSymes.GTRP.SyncVarAttribute");

            // Based off of https://medium.com/@EnescanBektas/using-source-generators-in-the-unity-game-engine-140ff0cd0dc
            foreach (IGrouping<INamedTypeSymbol, IFieldSymbol> group in syntaxReceiver.syncVars
                 .GroupBy<IFieldSymbol, INamedTypeSymbol>(f => f.ContainingType, SymbolEqualityComparer.Default))
            {
                string classSource = AddClass(group.Key, group, attributeSymbol);
                context.AddSource(group.Key.Name + "_NetworkBehaviour_Syncing.cs", SourceText.From(classSource, Encoding.UTF8));
            }

            if (syntaxReceiver.debug)
            {
                StringBuilder sourceBuilder = new StringBuilder(
                @"
using System;

public class DebugNetworkBehaviourSyncGenerator
{
     public static string GetTestText() 
     {
        return ""This is debug output from NetworkBehaviourSyncGenerator"";
     }
}
");
                sourceBuilder.Append("\n// " + System.DateTime.Now.ToString());
                sourceBuilder.Append(" count: " + syntaxReceiver.syncVars.Count);
                foreach (string s in syntaxReceiver.debug_strings)
                    sourceBuilder.Append("\n// " + s);

                context.AddSource("DebugNetworkBehaviourSyncGenerator", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
            }
        }

        public string AddClass(INamedTypeSymbol classSymbol, IEnumerable<IFieldSymbol> fields, ISymbol attributeSymbol)
        {
            // Class_Pre
            StringBuilder source = new StringBuilder($@"using System;
using UnityEngine;
using KitSymes.GTRP.Packets;

// This is an automatically generated class to handle the synchronisation of SyncVars
public partial class {classSymbol.Name} 
{{

");

            source.Append(AddInitialisation(fields));
            source.Append(AddDifferenceChecker(fields));
            source.Append(AddCreateDynamicSyncPacket(fields));
            source.Append(AddCreateFullSyncPacket(fields));
            source.Append(AddParseSyncPacket(fields));
            // Class_Post
            source.Append(@"
}");

            return source.ToString();
        }

        public static string Initialise_Pre = @"
    public override void Initialise()
    {
        base.Initialise();

";
        public static string Initialise_Post = @"    }
";

        private string AddInitialisation(IEnumerable<IFieldSymbol> fields)
        {
            StringBuilder stringBuilder = new StringBuilder();

            // Add the {field.Name}Old and _{field.Name}Changed variables
            stringBuilder.Append("#region Old and Changed Variables"); // Append and not AppendLine to make the formatting even out
            foreach (IFieldSymbol field in fields)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine($"    private {field.Type} _{field.Name}Old;");
                stringBuilder.AppendLine($"    private bool _{field.Name}Changed = false;");
            }
            stringBuilder.AppendLine("#endregion");

            // Add the Initialise Function
            stringBuilder.Append(Initialise_Pre);

            foreach (IFieldSymbol field in fields)
                stringBuilder.AppendLine($"        _{field.Name}Old = {field.Name};");

            stringBuilder.Append(Initialise_Post);

            return stringBuilder.ToString();
        }

        public static string DiffCheck_Pre = @"
    public override bool HasChanged()
    {
        bool changed = base.HasChanged();
";
        public static string DiffCheck_Post = @"
        return changed;
    }
";

        private string AddDifferenceChecker(IEnumerable<IFieldSymbol> fields)
        {
            StringBuilder stringBuilder = new StringBuilder(DiffCheck_Pre);

            // Add an if changed check for each field
            foreach (IFieldSymbol field in fields)
            {
                stringBuilder.AppendLine($@"
        if ({field.Name} != _{field.Name}Old)
        {{
            _{field.Name}Changed = true;
            _{field.Name}Old = {field.Name};
            changed = true;
        }}");
            }

            stringBuilder.Append(DiffCheck_Post);

            return stringBuilder.ToString();
        }

        public static string CreateDynamicSyncPacket_Pre = @"
    public override PacketNetworkBehaviourSync CreateDynamicSyncPacket()
    {
        PacketNetworkBehaviourSync packet = base.CreateDynamicSyncPacket();

        {
";
        public static string CreateDynamicSyncPacket_Post = @"
        return packet;
    }
";

        private string AddCreateDynamicSyncPacket(IEnumerable<IFieldSymbol> fields)
        {
            StringBuilder stringBuilder = new StringBuilder(CreateDynamicSyncPacket_Pre);

            // The bit count starts at 0, therefore bits 0-7 are in the first byte, 8-15 in the second, and so on
            int bitCount = 0;
            // Add the ConfigBytes, bytes containing a bool bit representing if this field is contained in the packet
            foreach (IFieldSymbol field in fields)
            {
                // If the bit is a multiple of 8, we need to start a new byte
                if (bitCount % 8 == 0)
                {
                    // If the bit is not the first, we need to cap off the last byte
                    if (bitCount > 0)
                    {
                        stringBuilder.Append(@"
            packet.dataList.Add(configByte);
        }
        {
");
                    }
                    // Create new config byte
                    stringBuilder.AppendLine("            byte configByte = 0;");
                    stringBuilder.AppendLine();
                }

                stringBuilder.AppendLine($"            if (_{field.Name}Changed) configByte |= 1 << {bitCount % 8};");
                bitCount++;
            }
            // End the last config byte
            stringBuilder.Append(@"            packet.dataList.Add(configByte);
        }
");

            // Add checks for each field, and add them to the packet if they have changed
            foreach (IFieldSymbol field in fields)
            {
                // The check to see if this field has changed this frame
                stringBuilder.Append($@"
        if (_{field.Name}Changed)
        {{
            _{field.Name}Changed = false;
");
                // Check to see if this field is a type we know how to handle
                switch (field.Type.SpecialType)
                {
                    case SpecialType.System_Char:
                    case SpecialType.System_Boolean:
                    case SpecialType.System_Int16:
                    case SpecialType.System_Int32:
                    case SpecialType.System_Int64:
                    case SpecialType.System_UInt16:
                    case SpecialType.System_UInt32:
                    case SpecialType.System_UInt64:
                    case SpecialType.System_Single:
                    case SpecialType.System_Double:
                        stringBuilder.AppendLine($"            packet.dataList.AddRange(BitConverter.GetBytes({field.Name}));");
                        break;
                    case SpecialType.System_Byte:
                        stringBuilder.AppendLine($"            packet.dataList.Add({field.Name});");
                        break;
                    default:
                        stringBuilder.AppendLine($"            Debug.LogError(\"Unsupported type {field.Type}\");");
                        break;
                }
                stringBuilder.Append(@"        }
");
            }

            stringBuilder.Append(CreateDynamicSyncPacket_Post);

            return stringBuilder.ToString();
        }

        public static string CreateFullSyncPacket_Pre = @"
    public override PacketNetworkBehaviourSync CreateFullSyncPacket()
    {
        PacketNetworkBehaviourSync packet = base.CreateFullSyncPacket();

";
        public static string CreateFullSyncPacket_Post = @"
        return packet;
    }
";

        private string AddCreateFullSyncPacket(IEnumerable<IFieldSymbol> fields)
        {
            StringBuilder stringBuilder = new StringBuilder(CreateFullSyncPacket_Pre);

            // The bit count starts at 0, therefore bits 0-7 are in the first byte, 8-15 in the second, and so on
            int bitCount = 0;
            // Add the ConfigBytes, bytes containing a bool bit representing if this field is contained in the packet
            foreach (IFieldSymbol field in fields)
            {
                // If the bit is a multiple of 8, we need to add a new byte
                if (bitCount % 8 == 0)
                    // Add a byte representing "All values are contained"
                    // The byte will be all 1's, even if there aren't that many fields, but that does not affect reading
                    stringBuilder.AppendLine("        packet.dataList.Add(255);");
                bitCount++;
            }
            stringBuilder.AppendLine();

            // Add each field to the packet
            foreach (IFieldSymbol field in fields)
                // Check to see if this field is a type we know how to handle
                switch (field.Type.SpecialType)
                {
                    case SpecialType.System_Char:
                    case SpecialType.System_Boolean:
                    case SpecialType.System_Int16:
                    case SpecialType.System_Int32:
                    case SpecialType.System_Int64:
                    case SpecialType.System_UInt16:
                    case SpecialType.System_UInt32:
                    case SpecialType.System_UInt64:
                    case SpecialType.System_Single:
                    case SpecialType.System_Double:
                        stringBuilder.AppendLine($"        packet.dataList.AddRange(BitConverter.GetBytes({field.Name}));");
                        break;
                    case SpecialType.System_Byte:
                        stringBuilder.AppendLine($"        packet.dataList.Add({field.Name});");
                        break;
                    default:
                        stringBuilder.AppendLine($"        Debug.LogError(\"Unsupported type {field.Type}\");");
                        break;
                }

            stringBuilder.Append(CreateFullSyncPacket_Post);

            return stringBuilder.ToString();
        }

        public static string ParseSyncPacket_Pre = @"
    public override int ParseSyncPacket(PacketNetworkBehaviourSync packet)
    {
        int pointer = base.ParseSyncPacket(packet);

";
        public static string ParseSyncPacket_Post = @"
        return pointer;
    }
";

        private string AddParseSyncPacket(IEnumerable<IFieldSymbol> fields)
        {
            StringBuilder stringBuilder = new StringBuilder(ParseSyncPacket_Pre);

            int configByteCount = ((fields.Count() - 1) / 8) + 1;

            stringBuilder.Append($@"        byte[] configBytes = new byte[{configByteCount}];
        for (int i = 0; i < {configByteCount}; i++)
            configBytes[i] = packet.data[pointer++];
");

            int bitCount = 0;
            foreach (IFieldSymbol field in fields)
            {
                stringBuilder.Append($@"
        if (((configBytes[{bitCount / 8}] >> {bitCount % 8}) & 1) != 0)
        {{
");
                switch (field.Type.SpecialType)
                {
                    case SpecialType.System_Boolean:
                        stringBuilder.AppendLine($"            {field.Name} = BitConverter.ToBoolean(packet.data, pointer);");
                        stringBuilder.AppendLine($"            pointer += {sizeof(bool)};");
                        break;
                    case SpecialType.System_Int16:
                        stringBuilder.AppendLine($"            {field.Name} = BitConverter.ToInt16(packet.data, pointer);");
                        stringBuilder.AppendLine($"            pointer += {sizeof(short)};");
                        break;
                    case SpecialType.System_Int32:
                        stringBuilder.AppendLine($"            {field.Name} = BitConverter.ToInt32(packet.data, pointer);");
                        stringBuilder.AppendLine($"            pointer += {sizeof(int)};");
                        break;
                    case SpecialType.System_Int64:
                        stringBuilder.AppendLine($"            {field.Name} = BitConverter.ToInt64(packet.data, pointer);");
                        stringBuilder.AppendLine($"            pointer += {sizeof(long)};");
                        break;
                    case SpecialType.System_UInt16:
                        stringBuilder.AppendLine($"            {field.Name} = BitConverter.ToUInt16(packet.data, pointer);");
                        stringBuilder.AppendLine($"            pointer += {sizeof(ushort)};");
                        break;
                    case SpecialType.System_UInt32:
                        stringBuilder.AppendLine($"            {field.Name} = BitConverter.ToUInt32(packet.data, pointer);");
                        stringBuilder.AppendLine($"            pointer += {sizeof(uint)};");
                        break;
                    case SpecialType.System_UInt64:
                        stringBuilder.AppendLine($"            {field.Name} = BitConverter.ToUInt64(packet.data, pointer);");
                        stringBuilder.AppendLine($"            pointer += {sizeof(ulong)};");
                        break;
                    case SpecialType.System_Single:
                        stringBuilder.AppendLine($"            {field.Name} = BitConverter.ToSingle(packet.data, pointer);");
                        stringBuilder.AppendLine($"            pointer += {sizeof(float)};");
                        break;
                    case SpecialType.System_Double:
                        stringBuilder.AppendLine($"            {field.Name} = BitConverter.ToDouble(packet.data, pointer);");
                        stringBuilder.AppendLine($"            pointer += {sizeof(double)};");
                        break;
                    case SpecialType.System_Char:
                        stringBuilder.AppendLine($"            {field.Name} = BitConverter.ToChar(packet.data, pointer);");
                        stringBuilder.AppendLine($"            pointer += {sizeof(char)};");
                        break;
                    case SpecialType.System_Byte:
                        stringBuilder.AppendLine($"            {field.Name} = packet.data[pointer];");
                        stringBuilder.AppendLine($"            pointer += {sizeof(byte)};");
                        break;
                    default:
                        stringBuilder.AppendLine($"            Debug.LogError(\"Unsupported type {field.Type}\");");
                        break;
                }
                stringBuilder.Append(@"        }
");
                bitCount++;
            }

            stringBuilder.Append(ParseSyncPacket_Post);

            return stringBuilder.ToString();
        }
    }
}
