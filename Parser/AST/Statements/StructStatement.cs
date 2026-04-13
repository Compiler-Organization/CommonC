using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class StructStatement : Statement
    {
        public string Name { get; set; } = "";

        public List<VariableDeclarationStatement> Fields { get; set; } = new List<VariableDeclarationStatement>();
    }
}
