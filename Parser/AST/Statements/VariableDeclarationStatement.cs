using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using CommonC.Parser.AST.Expressions;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class VariableDeclarationStatement : Statement
    {
        // Core AST properties
        public Expression Type { get; set; } = null!;

        public string Name { get; set; } = "";

        public Expression? Expression { get; set; } = null;

        // Internals for CIL code genration
        public CilLocalVariable CilLocalVariable { get; set; } = null!;

        // Semantics
        public bool IsGlobal { get; set; } = false;

        public bool IsParameter { get; set; } = false;

        public int ParameterIndex { get; set; } = 0;

        public bool IsField { get; set; } = false;

        public FieldDefinition Field { get; set; } = null!;

        public int FieldIndex { get; set; } = 0;

        // Liveness


        // Internals for LLVM code generation
        internal LLVMValueRef LLVMAlloca;

        internal LLVMTypeRef LLVMType;

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));
            Builder.Append(Type.ToString());
            Builder.Append(" ");
            Builder.Append(Name);
            if (Expression != null)
            {
                Builder.Append(" = ");
                Builder.Append(Expression.PrettyPrint(indentLevel + 1));
            }
            Builder.Append(";");
            Builder.Append(Environment.NewLine);

            return Builder.ToString();
        }
    }
}
