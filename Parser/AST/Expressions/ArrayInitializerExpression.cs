using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ArrayInitializerExpression : Expression
    {
        public IndexExpression Initializer { get; set; } = new IndexExpression();

        public ArrayExpression Array { get; set; } = new ArrayExpression();
    }
}
