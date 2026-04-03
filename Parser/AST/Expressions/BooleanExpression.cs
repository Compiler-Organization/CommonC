using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class BooleanExpression : Expression
    {
        public bool Value { get; set; }
    }
}
