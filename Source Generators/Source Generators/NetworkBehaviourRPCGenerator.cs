using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KitSymes.GTRP.SourceGenerators
{
    [Generator]
    public class NetworkBehaviourRPCGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new NetworkBehaviourRPCSyntaxContextReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            NetworkBehaviourRPCSyntaxContextReceiver syntaxReceiver = (NetworkBehaviourRPCSyntaxContextReceiver)context.SyntaxContextReceiver;

            // Based off of https://medium.com/@EnescanBektas/using-source-generators-in-the-unity-game-engine-140ff0cd0dc
            foreach (IGrouping<INamedTypeSymbol, IMethodSymbol> group in syntaxReceiver.clientRPCs
                 .GroupBy<IMethodSymbol, INamedTypeSymbol>(f => f.ContainingType, SymbolEqualityComparer.Default))
            {
                string classSource = AddClientRPCsClass(group.Key, group);
                context.AddSource(group.Key.Name + "_NetworkBehaviour_ClientRPCs.cs", SourceText.From(classSource, Encoding.UTF8));
            }

            // Based off of https://medium.com/@EnescanBektas/using-source-generators-in-the-unity-game-engine-140ff0cd0dc
            foreach (IGrouping<INamedTypeSymbol, IMethodSymbol> group in syntaxReceiver.serverRPCs
                 .GroupBy<IMethodSymbol, INamedTypeSymbol>(f => f.ContainingType, SymbolEqualityComparer.Default))
            {
                string classSource = AddServerRPCsClass(group.Key, group);
                context.AddSource(group.Key.Name + "_NetworkBehaviour_ServerRPCs.cs", SourceText.From(classSource, Encoding.UTF8));
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
                foreach (string s in syntaxReceiver.debug_strings)
                    sourceBuilder.Append("\n// " + s);

                context.AddSource($"Debug{GetType().Name}Generator", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
            }
        }

        public string AddClientRPCsClass(INamedTypeSymbol classSymbol, IEnumerable<IMethodSymbol> methods)
        {
            // Class_Pre
            StringBuilder source = new StringBuilder($@"using System.Collections.Generic;
using KitSymes.GTRP.Internal;
using KitSymes.GTRP.Packets;

// This is an automatically generated class to handle ClientRPCs
public partial class {classSymbol.Name} 
{{
    uint _clientRPCOffset = 0;
    
    public override uint InitialiseClientRPCs()
    {{
        _clientRPCOffset = base.InitialiseClientRPCs();

        return _clientRPCOffset + {methods.Count()};
    }}
");

            source.Append(AddRPCFunctions(methods, true));
            source.Append(AddParseClientRPCPacket(methods));
            // Class_Post
            source.Append(@"
}");

            return source.ToString();
        }

        public string AddServerRPCsClass(INamedTypeSymbol classSymbol, IEnumerable<IMethodSymbol> methods)
        {
            // Class_Pre
            StringBuilder source = new StringBuilder($@"using System.Collections.Generic;
using KitSymes.GTRP.Internal;
using KitSymes.GTRP.Packets;

// This is an automatically generated class to handle ServerRPCs
public partial class {classSymbol.Name} 
{{
    uint _serverRPCOffset = 0;
    
    public override uint InitialiseServerRPCs()
    {{
        _serverRPCOffset = base.InitialiseServerRPCs();

        return _serverRPCOffset + {methods.Count()};
    }}
");

            source.Append(AddRPCFunctions(methods, false));
            source.Append(AddParseServerRPCPacket(methods));
            // Class_Post
            source.Append(@"
}");

            return source.ToString();
        }

        private string AddRPCFunctions(IEnumerable<IMethodSymbol> methods, bool client)
        {
            StringBuilder stringBuilder = new StringBuilder();

            int id = 0;
            foreach (IMethodSymbol method in methods)
            {
                stringBuilder.Append($@"
    public void RPC{method.Name}(");

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    stringBuilder.Append($"{(i > 0 ? ", " : "")}{method.Parameters[i].Type} {method.Parameters[i].Name}");
                }


                stringBuilder.Append($@")
    {{
        List<byte> data = new List<byte>();

");
                foreach (IParameterSymbol parameter in method.Parameters)
                {
                    stringBuilder.AppendLine($"        data.AddRange(ByteConverter.SerialiseArgument({parameter.Name}));");
                }
                stringBuilder.Append($@"
        Packet{(client ? "Client" : "Server")}RPC packet = Create{(client ? "Client" : "Server")}RPCPacket(_{(client ? "client" : "server")}RPCOffset + {id}, data.ToArray());
        networkObject.AddTCPPacket(packet);
    }}
");
                id++;
            }

            return stringBuilder.ToString();
        }

        public static string ParseClientRPCPacket_Pre = @"
    public override void OnPacketClientRPCReceive(PacketClientRPC packet)
    {
";
        public static string ParseClientRPCPacket_Post = @"        else
            base.OnPacketClientRPCReceive(packet);
    }
";

        private string AddParseClientRPCPacket(IEnumerable<IMethodSymbol> methods)
        {
            StringBuilder stringBuilder = new StringBuilder(ParseClientRPCPacket_Pre);

            int id = 0;
            foreach (IMethodSymbol method in methods)
            {
                stringBuilder.Append($@"        {(id > 0 ? "else " : "")}if (packet.methodID == {id} + _clientRPCOffset)
        {{
");
                if (method.Parameters.Length > 0)
                    stringBuilder.AppendLine(@"            int pointer = 0;
");
                foreach (IParameterSymbol parameter in method.Parameters)
                    stringBuilder.AppendLine($"            {parameter.Type} {parameter.Name} = ({parameter.Type}) ByteConverter.DeserialiseArgument<{parameter.Type}>(packet.data, ref pointer);");
                stringBuilder.Append($@"
            {method.Name}(");
                for (int i = 0; i < method.Parameters.Length; i++)
                    stringBuilder.Append($"{(i > 0 ? ", " : "")}{method.Parameters[i].Name}");
                stringBuilder.Append(@");
        }
");
                id++;
            }

            stringBuilder.Append(ParseClientRPCPacket_Post);

            return stringBuilder.ToString();
        }

        public static string ParseServerRPCPacket_Pre = @"
    public override void OnPacketServerRPCReceive(PacketServerRPC packet)
    {
";
        public static string ParseServerRPCPacket_Post = @"        else
            base.OnPacketServerRPCReceive(packet);
    }
";

        private string AddParseServerRPCPacket(IEnumerable<IMethodSymbol> methods)
        {
            StringBuilder stringBuilder = new StringBuilder(ParseServerRPCPacket_Pre);

            int id = 0;
            foreach (IMethodSymbol method in methods)
            {
                stringBuilder.Append($@"        {(id > 0 ? "else " : "")}if (packet.methodID == {id} + _serverRPCOffset)
        {{
");
                if (method.Parameters.Length > 0)
                    stringBuilder.AppendLine(@"            int pointer = 0;
");
                foreach (IParameterSymbol parameter in method.Parameters)
                    stringBuilder.AppendLine($"            {parameter.Type} {parameter.Name} = ({parameter.Type}) ByteConverter.DeserialiseArgument<{parameter.Type}>(packet.data, ref pointer);");
                stringBuilder.Append($@"
            {method.Name}(");
                for (int i = 0; i < method.Parameters.Length; i++)
                    stringBuilder.Append($"{(i > 0 ? ", " : "")}{method.Parameters[i].Name}");
                stringBuilder.Append(@");
        }
");
                id++;
            }

            stringBuilder.Append(ParseServerRPCPacket_Post);

            return stringBuilder.ToString();
        }
    }
}
