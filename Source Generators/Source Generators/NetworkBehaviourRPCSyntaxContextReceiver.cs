using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace KitSymes.GTRP.SourceGenerators
{
    public class NetworkBehaviourRPCSyntaxContextReceiver : CustomSyntaxContextReceiver
    {
        public List<IMethodSymbol> clientRPCs = new List<IMethodSymbol>();
        public List<IMethodSymbol> serverRPCs = new List<IMethodSymbol>();

        // Based off of https://medium.com/@EnescanBektas/using-source-generators-in-the-unity-game-engine-140ff0cd0dc
        public override void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (!context.Node.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MethodDeclaration))
                return;

            MethodDeclarationSyntax methodDeclarationSyntax = (MethodDeclarationSyntax)context.Node;

            if (methodDeclarationSyntax.AttributeLists.Count <= 0)
                return;

            IMethodSymbol methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclarationSyntax) as IMethodSymbol;

            if (InheritsFrom(methodSymbol.ContainingType.BaseType, "KitSymes.GTRP.NetworkBehaviour"))
            {
                foreach (AttributeData attributeData in methodSymbol.GetAttributes())
                {
                    string name = attributeData.AttributeClass.ToDisplayString();
                    if (name == "KitSymes.GTRP.ClientRPCAttribute")
                        clientRPCs.Add(methodSymbol);
                    else if (name == "KitSymes.GTRP.ServerRPCAttribute")
                        serverRPCs.Add(methodSymbol);
                }
            }
        }

    }
}