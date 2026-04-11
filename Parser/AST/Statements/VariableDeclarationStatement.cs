using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
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

        public Expression? Expression { get; set; } = null;

        public CilLocalVariable CilLocalVariable { get; set; } = null!;

        public bool IsParameter { get; set; } = false;

        public int ParameterIndex { get; set; } = 0;

        public bool IsField { get; set; } = false;

        public FieldDefinition Field { get; set; } = null!;
    }
}
