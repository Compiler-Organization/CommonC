using CommonC.Error;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class ClassStatement : Statement
    {
        public string Name { get; set; } = "";

        public ClosureStatement Body { get; set; } = new ClosureStatement();

        internal StructStatement Struct { get; set; }

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));
            Builder.Append("class ");
            Builder.Append(Name);
            Builder.Append(" ");
            Builder.Append(Body.PrettyPrint(indentLevel));

            return Builder.ToString();
        }

        public VariableDeclarationStatement? GetField(string name)
        {
            return Body.Statements.OfType<VariableDeclarationStatement>().FirstOrDefault(v => v.Name == name);
        }

        public Variables GetFields()
        {
            return new Variables(Body.Statements.OfType<VariableDeclarationStatement>());
        }

        public List<FunctionDeclarationStatement> GetFunctions()
        {
            return Body.Statements.OfType<FunctionDeclarationStatement>().ToList();
        }
    }
}
