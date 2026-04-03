using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class CallExpression : Expression
    {
        public Expression Expression { get; set; } = null!;

        public ExpressionList Arguments { get; set; } = null!;
    }
}
