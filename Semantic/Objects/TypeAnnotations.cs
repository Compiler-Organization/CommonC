using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Semantic.Objects
{
    public class TypeAnnotations : List<TypeAnnotation>
    {
        public TypeAnnotations() { }

        public TypeAnnotations(ExpressionList expressions)
            => expressions.Select(e => e.TypeAnnotation).ToList();

        public override string ToString() 
            => string.Join(", ", this.Select(t => t.ToString()));
    }
}
