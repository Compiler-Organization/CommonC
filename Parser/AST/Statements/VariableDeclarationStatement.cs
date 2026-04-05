using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class VariableDeclarationStatement : Statement
    {
        public Expression Type { get; set; } = new Expression();

        public string Name { get; set; } = "";

        public Expression Expression { get; set; } = new Expression();
    }
}
