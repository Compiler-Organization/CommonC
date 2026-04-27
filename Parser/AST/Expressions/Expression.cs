using CommonC.Semantic.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class Expression
    {
        public TypeAnnotation TypeAnnotation { get; set; } = new TypeAnnotation();
    }
}
