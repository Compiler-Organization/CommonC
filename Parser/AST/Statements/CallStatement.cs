using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class CallStatement : Statement
    {
        public Expression Expression { get; set; } = null!;

        public ExpressionList Arguments { get; set; } = null!;
    }
}
