using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class Classes : List<ClassStatement>
    {
        public Classes() { }

        public Classes(List<ClassStatement> classes)
        {
            this.AddRange(classes);
        }

        public Classes(IEnumerable<ClassStatement> classes)
        {
            this.AddRange(classes);
        }

        public ClassStatement GetClass(string name)
        {
            List<ClassStatement> classes = [.. this.Where(v => v.Name == name)];

            if (classes.Count == 0)
            {
                throw new Exception($"Class {name} does not exist in the current scope: {string.Join(", ", this.Select(v => v.Name))}");
            }

            return classes.First();
        }

        public bool Contains(string name) => this.Any(v => v.Name == name);
    }
}
