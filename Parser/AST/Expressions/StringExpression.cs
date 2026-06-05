using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class StringExpression : Expression
    {
        public string Value { get; set; } = null!;

        public override string PrettyPrint(int indentLevel = 0)
        {
            return $"\"{this.Value}\"";
        }
    }
}
