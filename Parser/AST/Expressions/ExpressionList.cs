using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ExpressionList : List<Expression>
    {
        public bool IsLast(Expression e) => this.Count > 0 && this.Last() == e;
    }
}
