using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ParenthesizedExpression : Expression
    {
        public Expression Expression { get; set; } = new Expression();
    }
}
