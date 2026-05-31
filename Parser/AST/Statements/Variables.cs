using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class Variables : List<VariableDeclarationStatement>
    {
        public Variables() { }

        public Variables(List<VariableDeclarationStatement> variables)
        {
            this.AddRange(variables);
        }

        public Variables(IEnumerable<VariableDeclarationStatement> variables)
        {
            this.AddRange(variables);
        }

        public VariableDeclarationStatement GetVariable(string name)
        {
            List<VariableDeclarationStatement> variables = [.. this.Where(v => v.Name == name)];

            if(variables.Count == 0)
            {
                throw new Exception($"Variable {name} does not exist in the current scope: {string.Join(", ", this.Select(v => v.Name))}");
            }

            return variables.First();
        }

        public bool Contains(string name) => this.Any(v => v.Name == name);
    }
}
