using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Liveness.Statements
{
    /// <summary>
    /// <para>Used to determine when something should be freed.</para>
    /// <para>Created by liveness analysis exclusively.</para>
    /// </summary>
    public class FreeStatement : Statement
    {
        public Expression Expression;
    }
}
