using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Semantic
{
    public class SemanticAnalyzer
    {
        public void Analyze(StatementList statementList)
        {
            PassVariablesToInnerScope(statementList, new List<VariableDeclarationStatement>());
        }

        void PassVariablesToInnerScope(StatementList statementList, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            foreach (Statement statement in statementList)
            {
                if (statement is FunctionDeclarationStatement functionDeclarationStatement)
                {
                    foreach (VariableDeclarationStatement variableDeclarationStatement in functionDeclarationStatement.Body.Statements.OfType<VariableDeclarationStatement>())
                    {
                        functionDeclarationStatement.Body.Locals.Add(variableDeclarationStatement);
                    }
                    functionDeclarationStatement.Body.Locals.AddRange(variableDeclarationStatements);

                    for(int i = 0; i < functionDeclarationStatement.Parameters.Count; i++)
                    {
                        functionDeclarationStatement.Body.Locals.Add(new VariableDeclarationStatement
                        {
                            Name = functionDeclarationStatement.Parameters[i].Name,
                            Type = functionDeclarationStatement.Parameters[i].Type,
                            Expression = functionDeclarationStatement.Parameters[i].Value,
                            isParameter = true,
                            parameterIndex = i
                        });
                    }

                    PassVariablesToInnerScope(functionDeclarationStatement.Body.Statements, functionDeclarationStatement.Body.Locals);
                }

                if (statement is IfStatement ifStatement)
                {
                    foreach (VariableDeclarationStatement variableDeclarationStatement in ifStatement.Body.Statements.OfType<VariableDeclarationStatement>())
                    {
                        ifStatement.Body.Locals.Add(variableDeclarationStatement);
                    }

                    ifStatement.Body.Locals.AddRange(variableDeclarationStatements);
                    PassVariablesToInnerScope(ifStatement.Body.Statements, ifStatement.Body.Locals);

                    foreach(IfStatement elseIf in ifStatement.ElseIfs)
                    {
                        foreach (VariableDeclarationStatement variableDeclarationStatement in elseIf.Body.Statements.OfType<VariableDeclarationStatement>())
                        {
                            elseIf.Body.Locals.Add(variableDeclarationStatement);
                        }
                        elseIf.Body.Locals.AddRange(variableDeclarationStatements);
                        PassVariablesToInnerScope(elseIf.Body.Statements, elseIf.Body.Locals);
                    }

                    if(ifStatement.Else.Statements.Count() > 0)
                    {
                        foreach (VariableDeclarationStatement variableDeclarationStatement in ifStatement.Else.Statements.OfType<VariableDeclarationStatement>())
                        {
                            ifStatement.Else.Locals.Add(variableDeclarationStatement);
                        }
                        ifStatement.Else.Locals.AddRange(variableDeclarationStatements);
                        PassVariablesToInnerScope(ifStatement.Else.Statements, ifStatement.Else.Locals);
                    }
                }

                if(statement is ForStatement forStatement)
                {
                    foreach (VariableDeclarationStatement variableDeclarationStatement in forStatement.Body.Statements.OfType<VariableDeclarationStatement>())
                    {
                        forStatement.Body.Locals.Add(variableDeclarationStatement);
                    }

                    forStatement.Body.Locals.Add(forStatement.Variable);

                    forStatement.Body.Locals.AddRange(variableDeclarationStatements);
                    PassVariablesToInnerScope(forStatement.Body.Statements, forStatement.Body.Locals);
                }

                if (statement is WhileStatement whileStatement)
                {
                    foreach (VariableDeclarationStatement variableDeclarationStatement in whileStatement.Body.Statements.OfType<VariableDeclarationStatement>())
                    {
                        whileStatement.Body.Locals.Add(variableDeclarationStatement);
                    }

                    whileStatement.Body.Locals.AddRange(variableDeclarationStatements);
                    PassVariablesToInnerScope(whileStatement.Body.Statements, whileStatement.Body.Locals);
                }

                if (statement is ClosureStatement closureStatement)
                {
                    foreach (VariableDeclarationStatement variableDeclarationStatement in closureStatement.Statements.OfType<VariableDeclarationStatement>())
                    {
                        closureStatement.Locals.Add(variableDeclarationStatement);
                    }

                    closureStatement.Locals.AddRange(variableDeclarationStatements);
                    PassVariablesToInnerScope(closureStatement.Statements, closureStatement.Locals);
                }
            }
        }

        void AnnotateTypes()
        {
            
        }
    }
}
