using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class Structs : List<StructStatement>
    {
        public Structs() { }

        public Structs(List<StructStatement> structs)
        {
            this.AddRange(structs);
        }

        public Structs(IEnumerable<StructStatement> structs)
        {
            this.AddRange(structs);
        }

        public StructStatement GetStruct(string name)
        {
            List<StructStatement> structs = [.. this.Where(v => v.Name == name)];

            if (structs.Count == 0)
            {
                throw new Exception($"Struct {name} does not exist in the current scope: {string.Join(", ", this.Select(v => v.Name))}");
            }

            return structs.First();
        }

        public bool Contains(string name) => this.Any(v => v.Name == name);
    }
}
