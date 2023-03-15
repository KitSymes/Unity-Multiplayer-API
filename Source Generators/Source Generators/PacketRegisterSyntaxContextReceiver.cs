using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace KitSymes.GTRP.SourceGenerators
{
    public class PacketRegisterSyntaxContextReceiver : CustomSyntaxContextReceiver
    {
        public List<INamedTypeSymbol> packets = new List<INamedTypeSymbol>();

        public override void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (!context.Node.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ClassDeclaration))
                return;

            ClassDeclarationSyntax classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;

            INamedTypeSymbol symbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol;

            if (InheritsFrom(symbol.BaseType, "KitSymes.GTRP.Packet"))
                packets.Add(symbol);
        }
    }
}
