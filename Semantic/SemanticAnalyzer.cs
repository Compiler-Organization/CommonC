using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Semantic
{
    public class SemanticAnalyzer
    {
        public void PassVariablesToInnerScope(StatementList statementList, List<VariableDeclarationStatement> variableDeclarationStatements)
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
                }
            }
        }
    }
}
