using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class BooleanExpression : Expression
    {
        public bool Value { get; set; }

        public override string PrettyPrint(int indentLevel = 0)
        {
            return Value.ToString().ToLower();
        }
    }
}
