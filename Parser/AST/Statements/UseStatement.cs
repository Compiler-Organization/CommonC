using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class UseStatement : Statement
    {
        public IdentifierExpression Identifier { get; set; } = new IdentifierExpression();
    }
}
