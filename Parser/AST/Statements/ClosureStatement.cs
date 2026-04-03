using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    /// <summary>
    /// Closure statement (E.g: { log("hello, world!") })
    /// </summary>
    public class ClosureStatement : Statement
    {
        public StatementList Statements { get; set; } = null!;
    }
}
