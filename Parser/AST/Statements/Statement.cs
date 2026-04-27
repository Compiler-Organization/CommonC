using CommonC.Semantic.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class Statement
    {
        public TypeAnnotation TypeAnnotation { get; set; } = new TypeAnnotation();
    }
}
