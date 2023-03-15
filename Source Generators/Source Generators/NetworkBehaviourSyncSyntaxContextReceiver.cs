using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace KitSymes.GTRP.SourceGenerators
{
    public class NetworkBehaviourSyncSyntaxContextReceiver : CustomSyntaxContextReceiver
    {
        public List<IFieldSymbol> syncVars = new List<IFieldSymbol>();

        // Based off of https://medium.com/@EnescanBektas/using-source-generators-in-the-unity-game-engine-140ff0cd0dc
        public override void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (!context.Node.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.FieldDeclaration))
                return;

            FieldDeclarationSyntax fieldDeclarationSyntax = (FieldDeclarationSyntax)context.Node;

            if (fieldDeclarationSyntax.AttributeLists.Count <= 0)
                return;

            foreach (VariableDeclaratorSyntax variable in fieldDeclarationSyntax.Declaration.Variables)
            {
                IFieldSymbol fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;

                if (InheritsFrom(fieldSymbol.ContainingType.BaseType, "KitSymes.GTRP.NetworkBehaviour"))
                {
                    foreach (AttributeData attributeData in fieldSymbol.GetAttributes())
                    {
                        string name = attributeData.AttributeClass.ToDisplayString();
                        if (name == "KitSymes.GTRP.SyncVarAttribute")
                            syncVars.Add(fieldSymbol);
                    }
                }
            }
        }

    }
}