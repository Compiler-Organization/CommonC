using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class NumberExpression : Expression
    {
        public string Value { get; set; } = null!;
    }
}
