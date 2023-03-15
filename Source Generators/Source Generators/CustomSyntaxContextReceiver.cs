using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace KitSymes.GTRP.SourceGenerators
{
    public abstract class CustomSyntaxContextReceiver : ISyntaxContextReceiver
    {
        public bool debug = false;
        public List<string> debug_strings = new List<string>();

        public abstract void OnVisitSyntaxNode(GeneratorSyntaxContext context);

        public bool InheritsFrom(ITypeSymbol symbol, string target)
        {
            if (symbol.ToDisplayString().Equals(target))
                return true;
            if (symbol.BaseType != null)
                return InheritsFrom(symbol.BaseType, target);
            return false;
        }
    }
}
