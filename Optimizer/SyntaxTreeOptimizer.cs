using CommonC.Parser.AST.Statements;
using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace CommonC.Optimizer
{
    public class SyntaxTreeOptimizer
    {
        StatementList Statements { get; set; }

        public SyntaxTreeOptimizer(StatementList statements)
        {
            Statements = statements;
        }

        // TODO: Tail recursion optimization, etc.
        public void Optimize()
        {
        }
    }
}
