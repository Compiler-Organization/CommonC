using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class NegateExpression : Expression
    {
        public Expression Expression { get; set; } = new Expression();
    }
}
