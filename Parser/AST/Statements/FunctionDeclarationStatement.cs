using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class FunctionDeclarationStatement : Statement
    {
        public Expression ReturnType { get; set; } = new Expression();

        public string Name { get; set; } = "";

        public ExpressionList? Parameters { get; set; }

        public ClosureStatement? Body { get; set; }
    }
}
