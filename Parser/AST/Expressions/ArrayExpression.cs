using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ArrayExpression : Expression
    {
        public ExpressionList Expressions { get; set; } = new ExpressionList();
    }
}
