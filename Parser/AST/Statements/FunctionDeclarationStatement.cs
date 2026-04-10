using AsmResolver.DotNet;
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

        public List<ParameterExpression> Parameters { get; set; } = new List<ParameterExpression>();

        public ClosureStatement? Body { get; set; }

        internal MethodDefinition? Method { get; set; }
    }
}
