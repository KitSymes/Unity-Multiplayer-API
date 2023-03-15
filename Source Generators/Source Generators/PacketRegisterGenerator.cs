using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace KitSymes.GTRP.SourceGenerators
{
    [Generator]
    public class PacketRegisterGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new PacketRegisterSyntaxContextReceiver());
        }

        public static string PacketRegister_Pre = @"using UnityEngine;
using KitSymes.GTRP.Internal;

// This is an automatically generated class to register your packets with the API
public class PacketRegister
{
    [RuntimeInitializeOnLoadMethod]
    static void RegisterPackets()
    {
";
        public static string PacketRegister_Post = @"    }
}
";
        public void Execute(GeneratorExecutionContext context)
        {
            PacketRegisterSyntaxContextReceiver syntaxReceiver = (PacketRegisterSyntaxContextReceiver)context.SyntaxContextReceiver;

            if (syntaxReceiver.packets.Count > 0)
            {
                StringBuilder packetRegisterClass = new StringBuilder(PacketRegister_Pre);
                foreach (INamedTypeSymbol symbol in syntaxReceiver.packets)
                    packetRegisterClass.AppendLine($"        PacketFormatter.RegisterPacket(typeof({symbol}));");
                packetRegisterClass.Append(PacketRegister_Post);
                context.AddSource("PacketRegister", SourceText.From(packetRegisterClass.ToString(), Encoding.UTF8));
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
    }
}
