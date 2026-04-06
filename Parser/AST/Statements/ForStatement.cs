using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class ForStatement : Statement
    {
        public RangeExpression Range { get; set; } = new RangeExpression();

        public VariableDeclarationStatement Variable { get; set; } = new VariableDeclarationStatement();

        public ClosureStatement Body { get; set; } = new ClosureStatement();
    }
}
