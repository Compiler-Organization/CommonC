using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class IfStatement : Statement
    {
        public Expression Condition { get; set; } = new Expression();

        public ClosureStatement Body { get; set; } = new ClosureStatement();

        public List<IfStatement> ElseIfStatements { get; set; } = new List<IfStatement>();

        public ClosureStatement ElseStatements { get; set; } = new ClosureStatement();
    }
}
