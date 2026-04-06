using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class ReturnStatement : Statement
    {
        public Expression? Expression { get; set; } = null;
    }
}
