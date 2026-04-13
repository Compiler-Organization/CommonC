using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ObjectInitializerExpression : Expression
    {
        public Expression Expression = new Expression();

        public List<AssignmentStatement> PropertyAssignments = new List<AssignmentStatement>();
    }
}
