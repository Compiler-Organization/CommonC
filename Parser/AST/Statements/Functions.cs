using CommonC.Error;
using CommonC.Parser.AST.Expressions;
using CommonC.Semantic.Objects;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class Functions : List<FunctionDeclarationStatement>
    {
        public Functions() { }

        public Functions(List<FunctionDeclarationStatement> functions)
        {
            this.AddRange(functions);
        }

        public Functions(IEnumerable<FunctionDeclarationStatement> functions)
        {
            this.AddRange(functions);
        }

        public void Add(FunctionDeclarationStatement functionDeclarationStatement, bool matchParameters = true)
        {
            if(matchParameters)
            {
                IEnumerable<FunctionDeclarationStatement> functions = this.Where(f => f.Name == functionDeclarationStatement.Name);
                if (functions.Any())
                {
                    foreach (FunctionDeclarationStatement function in functions)
                    {
                        if (function.Parameters.MatchTypes(functionDeclarationStatement.Parameters, false))
                        {
                            throw ErrorHandler.CreateError($"Function '{functionDeclarationStatement.Name}' already exists with overload:  {function.Name}({string.Join(", ", function.Parameters.Select(p => p.TypeAnnotation.ToString() + " " + p.Name))})", functionDeclarationStatement);
                        }
                    }
                }
            }

            this.AddRange(functionDeclarationStatement);
        }

        public FunctionDeclarationStatement GetFunction(string name)
        {
            List<FunctionDeclarationStatement> functions = [.. this.Where(v => v.Name == name)];

            if (functions.Count == 0)
            {
                throw new Exception($"Function {name} does not exist in the current scope: {string.Join(", ", this.Select(v => v.Name))}");
            }

            return functions.First();
        }

        public FunctionDeclarationStatement GetFunction(string name, ExpressionList? arguments, object errorObject)
        {
            List<FunctionDeclarationStatement> functions = [.. this.Where(f => f.Name == name)];

            if (!functions.Any())
                throw new Exception($"Function '{name}' does not exist");

            if(arguments == null || arguments.Count == 0)
            {
                return functions.First();
            }

            FunctionDeclarationStatement? function = null;
            foreach(FunctionDeclarationStatement functionDeclaration in functions) // TODO: Change up this so it properly respects default parameter assignments in functions
            {
                if (functionDeclaration.Parameters.MatchTypes(arguments, false))
                {
                    function = functionDeclaration;
                    break;
                }
            }

            if (function == null)
                throw ErrorHandler.CreateError($"No overload found for function '{name}({string.Join(", ", arguments.Select(a => a.TypeAnnotation.ToString()))})'", errorObject);

            return function;
        }

        public bool Contains(string name) => this.Any(f => f.Name == name);
    }
}
