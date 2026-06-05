using AsmResolver.DotNet;
using CommonC.Parser.AST.Expressions;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class FunctionDeclarationStatement : Statement
    {
        public Expression ReturnType { get; set; } = null!;

        public string Name { get; set; } = "";

        public bool IsExtern { get; set; } = false;

        public ParameterExpressionList Parameters { get; set; } = new ParameterExpressionList();

        public ClosureStatement? Body { get; set; }

        internal MethodDefinition? DotNetMethod { get; set; }

        internal LLVMValueRef LLVMFunction;

        internal LLVMTypeRef LLVMFunctionType { get; set; }

        internal LLVMValueRef ReturnReference { get; set; }

        internal LLVMBasicBlockRef ReturnBlock { get; set; }

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));

            if (IsExtern)
            {
                Builder.Append("extern ");
            }

            Builder.Append(ReturnType.PrettyPrint());
            Builder.Append(" ");
            Builder.Append(Name);
            if (Parameters != null && Parameters.Count > 0)
            {
                Builder.Append("(");
                Builder.Append(Parameters.PrettyPrint(indentLevel));
                if (Parameters.IsVararg)
                {
                    Builder.Append(", ...");
                }
                Builder.Append(")");
            }

            if (Body != null)
            {
                Builder.Append(Environment.NewLine);
                Builder.Append(Body.PrettyPrint(indentLevel));
            }
            else
            {
                Builder.Append(";");
                Builder.Append(Environment.NewLine);
            }

            return Builder.ToString();
        }
    }
}
