using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class StatementList : List<Statement>
    {
        public string PrettyPrint(int indentLevel)
            => string.Join("", this.Select(s => s.PrettyPrint(indentLevel)));

        public string ToString(bool newLine)
            => string.Join("", this.Select(s => s.ToString(newLine)));
    }
}
