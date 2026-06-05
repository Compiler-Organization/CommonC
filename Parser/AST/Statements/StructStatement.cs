using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class StructStatement : Statement
    {
        public string Name { get; set; } = "";

        public Variables Fields { get; set; } = new Variables();

        internal LLVMTypeRef LLVMStructType;

        internal LLVMValueRef LLVMStructPointer;

        internal LLVMValueRef LLVMStructGlobal;

        /// <summary>
        /// Gets the field with the given name, throws an exception if the field does not exist
        /// </summary>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public VariableDeclarationStatement GetField(string fieldName)
        {
            return Fields.GetVariable(fieldName) ?? throw new Exception($"Field {fieldName} does not exist in struct {Name}");
        }

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));
            Builder.Append("struct ");
            Builder.Append(Name);
            Builder.Append(" {");
            Builder.Append(Environment.NewLine);
            foreach (VariableDeclarationStatement variableDeclarationStatement in Fields)
            {
                Builder.Append(GetIndent(indentLevel + 1));

                Builder.Append(variableDeclarationStatement.Type.PrettyPrint(indentLevel + 1));
                Builder.Append(" ");
                Builder.Append(variableDeclarationStatement.Name);
                if (variableDeclarationStatement.Expression != null)
                {
                    Builder.Append(": ");
                    Builder.Append(variableDeclarationStatement.Expression.PrettyPrint(indentLevel + 1));
                }
                if (Fields.IndexOf(variableDeclarationStatement) != Fields.Count - 1)
                {
                    Builder.Append(",");
                }
                Builder.Append(Environment.NewLine);
            }
            Builder.Append(GetIndent(indentLevel));
            Builder.Append("}");
            Builder.Append(Environment.NewLine);

            return Builder.ToString();
        }
    }
}
