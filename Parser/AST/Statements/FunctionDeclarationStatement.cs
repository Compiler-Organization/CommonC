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
        public Expression ReturnType { get; set; } = new Expression();

        public string Name { get; set; } = "";

        public List<ParameterExpression> Parameters { get; set; } = new List<ParameterExpression>();

        public ClosureStatement? Body { get; set; }

        internal MethodDefinition? DotNetMethod { get; set; }

        internal LLVMValueRef LLVMFunction;

        internal LLVMTypeRef LLVMFunctionType { get; set; }

        internal LLVMValueRef ReturnReference { get; set; }

        internal LLVMBasicBlockRef ReturnBlock { get; set; }
    }
}
