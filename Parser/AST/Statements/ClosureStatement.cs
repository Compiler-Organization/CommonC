using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
	/// <summary>
	/// Closure statement (E.g: { print("hello, world!") })
	/// </summary>
	public class ClosureStatement : Statement
    {
        public StatementList Statements { get; set; } = new StatementList();

        public Variables Variables { get; set; } = new Variables();

        public Functions Functions { get; set; } = new Functions();

        public Structs Structs { get; set; } = new Structs();

        public Classes Classes { get; set; } = new Classes();

        public Enums Enums { get; set; } = new Enums();

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));
            Builder.Append("{");
            Builder.Append(Environment.NewLine);

            if (Statements != null)
            {
                Builder.Append(Statements.PrettyPrint(indentLevel + 1));
            }

            Builder.Append(GetIndent(indentLevel));
            Builder.Append("}");
            Builder.Append(Environment.NewLine);

            return Builder.ToString();
        }
    }
}
