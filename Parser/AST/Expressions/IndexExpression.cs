using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class IndexExpression : Expression
    {
        public Expression Expression { get; set; } = new Expression();

        public Expression Index { get; set; } = new Expression();
    }
}
