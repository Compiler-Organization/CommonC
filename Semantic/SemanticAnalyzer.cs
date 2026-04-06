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
                        functionDeclarationStatement.Body.VariableDeclarations.Add(variableDeclarationStatement);
                    }
                    functionDeclarationStatement.Body.VariableDeclarations.AddRange(variableDeclarationStatements);

                    int parameterIndex = 0;

                    for(int i = 0; i < functionDeclarationStatement.Parameters.Count; i++)
                    {
                        functionDeclarationStatement.Body.VariableDeclarations.Add(new VariableDeclarationStatement
                        {
                            Name = functionDeclarationStatement.Parameters[i].Name,
                            Type = functionDeclarationStatement.Parameters[i].Type,
                            Expression = functionDeclarationStatement.Parameters[i].Value,
                            isParameter = true,
                            parameterIndex = i
                        });
                    }

                    PassVariablesToInnerScope(functionDeclarationStatement.Body.Statements, functionDeclarationStatement.Body.VariableDeclarations);
                }

                if (statement is IfStatement ifStatement)
                {
                    foreach (VariableDeclarationStatement variableDeclarationStatement in ifStatement.Body.Statements.OfType<VariableDeclarationStatement>())
                    {
                        ifStatement.Body.VariableDeclarations.Add(variableDeclarationStatement);
                    }

                    ifStatement.Body.VariableDeclarations.AddRange(variableDeclarationStatements);
                    PassVariablesToInnerScope(ifStatement.Body.Statements, ifStatement.Body.VariableDeclarations);

                    foreach(IfStatement elseIf in ifStatement.ElseIfs)
                    {
                        foreach (VariableDeclarationStatement variableDeclarationStatement in elseIf.Body.Statements.OfType<VariableDeclarationStatement>())
                        {
                            elseIf.Body.VariableDeclarations.Add(variableDeclarationStatement);
                        }
                        elseIf.Body.VariableDeclarations.AddRange(variableDeclarationStatements);
                        PassVariablesToInnerScope(elseIf.Body.Statements, elseIf.Body.VariableDeclarations);
                    }

                    if(ifStatement.Else.Statements.Count() > 0)
                    {
                        foreach (VariableDeclarationStatement variableDeclarationStatement in ifStatement.Else.Statements.OfType<VariableDeclarationStatement>())
                        {
                            ifStatement.Else.VariableDeclarations.Add(variableDeclarationStatement);
                        }
                        ifStatement.Else.VariableDeclarations.AddRange(variableDeclarationStatements);
                        PassVariablesToInnerScope(ifStatement.Else.Statements, ifStatement.Else.VariableDeclarations);
                    }
                }

                if(statement is ForStatement forStatement)
                {
                    foreach (VariableDeclarationStatement variableDeclarationStatement in forStatement.Body.Statements.OfType<VariableDeclarationStatement>())
                    {
                        forStatement.Body.VariableDeclarations.Add(variableDeclarationStatement);
                    }

                    forStatement.Body.VariableDeclarations.Add(forStatement.Variable);

                    forStatement.Body.VariableDeclarations.AddRange(variableDeclarationStatements);
                    PassVariablesToInnerScope(forStatement.Body.Statements, forStatement.Body.VariableDeclarations);
                }

                if(statement is ClosureStatement closureStatement)
                {
                    foreach (VariableDeclarationStatement variableDeclarationStatement in closureStatement.Statements.OfType<VariableDeclarationStatement>())
                    {
                        closureStatement.VariableDeclarations.Add(variableDeclarationStatement);
                    }

                    closureStatement.VariableDeclarations.AddRange(variableDeclarationStatements);
                    PassVariablesToInnerScope(closureStatement.Statements, closureStatement.VariableDeclarations);
                }
            }
        }
    }
}
