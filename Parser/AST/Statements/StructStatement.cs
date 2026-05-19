using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class StructStatement : Statement
    {
        public string Name { get; set; } = "";

        public List<VariableDeclarationStatement> Fields { get; set; } = new List<VariableDeclarationStatement>();

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
            List<VariableDeclarationStatement> fields = Fields.Where(f => f.Name == fieldName).ToList();

            if (!fields.Any())
            {
                throw new Exception($"Field {fieldName} does not exist in struct {Name}");
            }

            return fields.First();
        }
    }
}
