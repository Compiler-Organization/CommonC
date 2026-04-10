using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class WhileStatement : Statement
    {
        public Expression Expression { get; set; } = new Expression();

        public ClosureStatement Body { get; set; } = new ClosureStatement();
    }
}
