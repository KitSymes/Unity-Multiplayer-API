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
                @"using System;

public class Debug" + GetType().Name + @"
{
     public static string GetTestText() 
     {
        return ""This is debug output"";
     }
}
");
                sourceBuilder.Append("\n// " + System.DateTime.Now.ToString());
                sourceBuilder.Append(" count: " + syntaxReceiver.syncVars.Count);
                foreach (string s in syntaxReceiver.debug_strings)
                    sourceBuilder.Append("\n// " + s);

                context.AddSource($"Debug{GetType().Name}Generator", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
            }
        }

        public string AddClass(INamedTypeSymbol classSymbol, IEnumerable<IFieldSymbol> fields, ISymbol attributeSymbol)
        {
            // Class_Pre
            StringBuilder source = new StringBuilder($@"using System.Collections.Generic;
using KitSymes.GTRP.Internal;
using KitSymes.GTRP.Packets;

// This is an automatically generated class to handle the synchronisation of SyncVars
public partial class {classSymbol.Name} 
{{

");

            source.Append(AddInitialisation(fields));
            source.Append(AddDifferenceChecker(fields, attributeSymbol));
            source.Append(AddGetDynamicData(fields));
            source.Append(AddGetFullData(fields));
            source.Append(AddParseSyncPacket(fields, attributeSymbol));
            // Class_Post
            source.Append(@"
}");

            return source.ToString();
        }

        public static string Initialise_Pre = @"
    public override void InitialiseSyncData()
    {
        base.InitialiseSyncData();

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

        private string AddDifferenceChecker(IEnumerable<IFieldSymbol> fields, ISymbol attributeSymbol)
        {
            StringBuilder stringBuilder = new StringBuilder(DiffCheck_Pre);

            // Add an if changed check for each field
            foreach (IFieldSymbol field in fields)
            {
                stringBuilder.AppendLine($@"
        if ({field.Name} != _{field.Name}Old)
        {{
            _{field.Name}Changed = true;
            changed = true;");

                foreach (AttributeData at in field.GetAttributes())
                {
                    if (at.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default))
                        if ((string)at.ConstructorArguments[0].Value != "")
                            stringBuilder.AppendLine($"            {at.ConstructorArguments[0].Value}(_{field.Name}Old, {field.Name});");
                }
                stringBuilder.AppendLine($@"
            _{field.Name}Old = {field.Name};
        }}");
            }

            stringBuilder.Append(DiffCheck_Post);

            return stringBuilder.ToString();
        }

        public static string GetDynamicData_Pre = @"
    protected override List<byte> GetDynamicData()
    {
        List<byte> list = base.GetDynamicData();

        {
";
        public static string GetDynamicData_Post = @"
        return list;
    }
";

        private string AddGetDynamicData(IEnumerable<IFieldSymbol> fields)
        {
            StringBuilder stringBuilder = new StringBuilder(GetDynamicData_Pre);

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
            list.Add(configByte);
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
            stringBuilder.Append(@"            list.Add(configByte);
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
                stringBuilder.AppendLine($"            list.AddRange(ByteConverter.SerialiseArgument<{field.Type}>({field.Name}));");
                stringBuilder.Append(@"        }
");
            }

            stringBuilder.Append(GetDynamicData_Post);

            return stringBuilder.ToString();
        }

        public static string GetFullData_Pre = @"
    protected override List<byte> GetFullData()
    {
        List<byte> list = base.GetFullData();

";
        public static string GetFullData_Post = @"
        return list;
    }
";

        private string AddGetFullData(IEnumerable<IFieldSymbol> fields)
        {
            StringBuilder stringBuilder = new StringBuilder(GetFullData_Pre);

            // The bit count starts at 0, therefore bits 0-7 are in the first byte, 8-15 in the second, and so on
            int bitCount = 0;
            // Add the ConfigBytes, bytes containing a bool bit representing if this field is contained in the packet
            foreach (IFieldSymbol field in fields)
            {
                // If the bit is a multiple of 8, we need to add a new byte
                if (bitCount % 8 == 0)
                    // Add a byte representing "All values are contained"
                    // The byte will be all 1's, even if there aren't that many fields, but that does not affect reading
                    stringBuilder.AppendLine("        list.Add(255);");
                bitCount++;
            }
            stringBuilder.AppendLine();

            // Add each field to the packet
            foreach (IFieldSymbol field in fields)
                stringBuilder.AppendLine($"        list.AddRange(ByteConverter.SerialiseArgument<{field.Type}>({field.Name}));");

            stringBuilder.Append(GetFullData_Post);

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

        private string AddParseSyncPacket(IEnumerable<IFieldSymbol> fields, ISymbol attributeSymbol)
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
                stringBuilder.AppendLine($@"
        if (((configBytes[{bitCount / 8}] >> {bitCount % 8}) & 1) != 0)
        {{");
                bool hasOnChangedCall = false;
                foreach (AttributeData at in field.GetAttributes())
                {
                    if (at.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default))
                        if ((string)at.ConstructorArguments[0].Value != "")
                        {
                            stringBuilder.AppendLine($@"            {field.Type} newValue = ({field.Type})ByteConverter.DeserialiseArgument<{field.Type}>(packet.data, ref pointer);");
                            stringBuilder.AppendLine($"            {at.ConstructorArguments[0].Value}(_{field.Name}Old, newValue);");
                            stringBuilder.AppendLine($@"            {field.Name} = newValue;");
                            hasOnChangedCall = true;
                        }
                }

                if (!hasOnChangedCall)
                    stringBuilder.AppendLine($"            {field.Name} = ({field.Type})ByteConverter.DeserialiseArgument<{field.Type}>(packet.data, ref pointer);");
                stringBuilder.AppendLine("        }");

                bitCount++;
            }

            stringBuilder.Append(ParseSyncPacket_Post);

            return stringBuilder.ToString();
        }
    }
}
