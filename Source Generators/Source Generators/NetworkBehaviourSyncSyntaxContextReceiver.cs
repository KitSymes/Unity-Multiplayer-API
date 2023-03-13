using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace KitSymes.GTRP.SourceGenerators
{
    public class NetworkBehaviourSyncSyntaxContextReceiver : ISyntaxContextReceiver
    {
        public List<IFieldSymbol> syncVars = new List<IFieldSymbol>();

        public bool debug = false;
        public List<string> debug_strings = new List<string>();

        // Based off of https://medium.com/@EnescanBektas/using-source-generators-in-the-unity-game-engine-140ff0cd0dc
        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (!context.Node.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.FieldDeclaration))
                return;

            FieldDeclarationSyntax fieldDeclarationSyntax = (FieldDeclarationSyntax)context.Node;

            if (fieldDeclarationSyntax.AttributeLists.Count <= 0)
                return;

            foreach (VariableDeclaratorSyntax variable in fieldDeclarationSyntax.Declaration.Variables)
            {
                IFieldSymbol fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;

                if (InheritsFrom(fieldSymbol.ContainingType.BaseType, "NetworkBehaviour"))
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

        public bool InheritsFrom(ITypeSymbol symbol, string target)
        {
            if (symbol.Name.Equals(target))
                return true;
            if (symbol.BaseType != null)
                return InheritsFrom(symbol.BaseType, target);
            return false;
        }
    }
}