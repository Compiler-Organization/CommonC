using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using CommonC.Semantic.Objects;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Semantic
{
    public class SemanticAnalyzer
    {
        ClosureStatement Closure { get; set; }

        public SemanticAnalyzer(ClosureStatement closure)
        {
            Closure = closure;
        }

        public void Analyze()
        {
            Closure.Variables = new Variables(Closure.Statements.OfType<VariableDeclarationStatement>());

            PassDefinitionsToInnerScopes(Closure, Closure);

            TypeTracker typeAnnotator = new TypeTracker();
            typeAnnotator.TrackTypes(Closure);
        }

        void PassDefinitionsToInnerScopes(ClosureStatement previousClosure, ClosureStatement closure)
        {
            foreach (VariableDeclarationStatement variableDeclarationStatement in closure.Statements.OfType<VariableDeclarationStatement>())
            {
                closure.Variables.Add(variableDeclarationStatement);
            }

            foreach (FunctionDeclarationStatement nestedFunctionDeclarationStatement in closure.Statements.OfType<FunctionDeclarationStatement>())
            {
                closure.Functions.Add(nestedFunctionDeclarationStatement, matchParameters: false);
            }

            foreach (StructStatement structStatement in closure.Statements.OfType<StructStatement>())
            {
                closure.Structs.Add(structStatement);
            }

            foreach (ClassStatement nestedClassStatement in closure.Statements.OfType<ClassStatement>())
            {
                closure.Classes.Add(nestedClassStatement);
            }

            foreach (EnumStatement enumStatement in closure.Statements.OfType<EnumStatement>())
            {
                closure.Enums.Add(enumStatement);
            }

            closure.Variables.AddRange(previousClosure.Variables);
            closure.Functions.AddRange(previousClosure.Functions);
            closure.Structs.AddRange(previousClosure.Structs);
            closure.Classes.AddRange(previousClosure.Classes);
            closure.Enums.AddRange(previousClosure.Enums);


            foreach (Statement statement in closure.Statements)
            {
                if (statement is FunctionDeclarationStatement functionDeclarationStatement)
                {
                    if(functionDeclarationStatement.Body == null)
                    {
                        continue;
                    }

                    for(int i = 0; i < functionDeclarationStatement.Parameters.Count; i++)
                    {
                        functionDeclarationStatement.Body.Variables.Add(new VariableDeclarationStatement
                        {
                            Name = functionDeclarationStatement.Parameters[i].Name,
                            Type = functionDeclarationStatement.Parameters[i].Type,
                            Expression = functionDeclarationStatement.Parameters[i].Value,
                            IsParameter = true,
                            ParameterIndex = i
                        });
                    }

                    PassDefinitionsToInnerScopes(closure, functionDeclarationStatement.Body);

                    continue;
                }

                if (statement is IfStatement ifStatement)
                {
                    PassDefinitionsToInnerScopes(closure, ifStatement.Body);

                    foreach (IfStatement elseIf in ifStatement.ElseIfs)
                    {
                        PassDefinitionsToInnerScopes(closure, elseIf.Body);
                    }

                    if(ifStatement.Else.Statements.Count() > 0)
                    {
                        PassDefinitionsToInnerScopes(closure, ifStatement.Else);
                    }
                    continue;
                }

                if(statement is ForStatement forStatement)
                {
                    forStatement.Body.Variables.Add(forStatement.Variable);
                    PassDefinitionsToInnerScopes(closure, forStatement.Body);
                    continue;
                }

                if(statement is ClassStatement classStatement)
                {
                    PassDefinitionsToInnerScopes(closure, classStatement.Body);
                    continue;
                }

                if (statement is WhileStatement whileStatement)
                {
                    PassDefinitionsToInnerScopes(closure, whileStatement.Body);
                    continue;
                }

                if (statement is ClosureStatement closureStatement)
                {
                    PassDefinitionsToInnerScopes(closure, closureStatement);
                    continue;
                }

                if(statement is SwitchStatement switchStatement)
                {
                    foreach(SwitchCase switchCase in switchStatement.Cases)
                    {
                        PassDefinitionsToInnerScopes(closure, switchCase.Body);
                    }

                    if(switchStatement.DefaultCase != null)
                    {
                        PassDefinitionsToInnerScopes(closure, switchStatement.DefaultCase.Body);
                    }
                }
            }
        }

        
    }
}
