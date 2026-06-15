using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class Enums : List<EnumStatement>
    {
        public Enums() { }

        public Enums(List<EnumStatement> enums)
        {
            this.AddRange(enums);
        }

        public Enums(IEnumerable<EnumStatement> enums)
        {
            this.AddRange(enums);
        }

        public EnumStatement GetClass(string name)
        {
            List<EnumStatement> enums = [.. this.Where(v => v.Name == name)];

            if (enums.Count == 0)
            {
                throw new Exception($"Class {name} does not exist in the current scope: {string.Join(", ", this.Select(v => v.Name))}");
            }

            return enums.First();
        }

        public bool Contains(string name) => this.Any(v => v.Name == name);
    }
}
