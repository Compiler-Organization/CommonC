using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class NumberExpression : Expression
    {
        public string Value { get; set; } = null!;

        public bool IsDouble { get; set; } = false;

        public override string PrettyPrint(int indentLevel = 0)
        {
            return Value;
        }
    }
}
