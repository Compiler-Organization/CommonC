using CommonC.Liveness.Statements;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonC.Liveness
{
    /*
    The idea for this will be an analyser that goes through every read / write to a variable / what would be memory, then insert a free at the last read/write to the memory location.
    * Functions that returns a variable, does not free the variable.
    * Need to take into account variables can be assigned conditionally. E.g "if var == true, bigVar = ...".

    */
    public class LivenessAnalyser
    {
        StatementList Statements { get; set; }
        HashSet<string> VariablesBeingReturned { get; set; } = new();

        public LivenessAnalyser(StatementList statements)
        {
            Statements = statements;
        }

        public void Analyse()
        {
            CollectReturnedVariables(Statements);

            Variables globalVariables = new Variables([.. Statements.OfType<VariableDeclarationStatement>()]);
            AnalyseStatements(Statements, globalVariables, new Variables());
        }

        void CollectReturnedVariables(StatementList statements)
        {
            foreach(Statement statement in statements)
            {
                if(statement is ReturnStatement returnStatement)
                {
                    if(returnStatement.Expression is IdentifierExpression identifierExpr)
                    {
                        VariablesBeingReturned.Add(identifierExpr.Name);
                    }
                }
                else if(statement is FunctionDeclarationStatement funcDecl)
                {
                    if(funcDecl.Body != null)
                    {
                        CollectReturnedVariables(funcDecl.Body.Statements);
                    }
                }
                else if(statement is IfStatement ifStmt)
                {
                    CollectReturnedVariables(ifStmt.Body.Statements);
                    foreach(var elseIf in ifStmt.ElseIfs)
                    {
                        CollectReturnedVariables(elseIf.Body.Statements);
                    }
                    if(ifStmt.Else != null)
                    {
                        CollectReturnedVariables(ifStmt.Else.Statements);
                    }
                }
                else if(statement is ForStatement forStmt)
                {
                    CollectReturnedVariables(forStmt.Body.Statements);
                }
                else if(statement is WhileStatement whileStmt)
                {
                    CollectReturnedVariables(whileStmt.Body.Statements);
                }
            }
        }

        void AnalyseStatements(StatementList statements, Variables allVisibleVariables, Variables locallyDeclaredVariables)
        {
            foreach(Statement statement in statements)
            {
                AnalyseStatement(statement, allVisibleVariables);
            }

            InsertFreesAtScopeExit(statements, locallyDeclaredVariables);
        }

        void AnalyseStatement(Statement statement, Variables variables)
        {
            if(statement is FunctionDeclarationStatement functionDeclarationStatement)
            {
                AnalyseFunctionDeclarationStatement(functionDeclarationStatement, variables);
                return;
            }

            if(statement is VariableDeclarationStatement variableDeclarationStatement)
            {
                AnalyseVariableDeclarationStatement(variableDeclarationStatement, variables);
                return;
            }

            if(statement is AssignmentStatement assignmentStatement)
            {
                AnalyseAssignmentStatement(assignmentStatement, variables);
                return;
            }

            if(statement is ReturnStatement returnStatement)
            {
                AnalyseReturnStatement(returnStatement, variables);
                return;
            }

            if(statement is IfStatement ifStatement)
            {
                AnalyseIfStatement(ifStatement, variables);
                return;
            }

            if(statement is ForStatement forStatement)
            {
                AnalyseForStatement(forStatement, variables);
                return;
            }

            if(statement is WhileStatement whileStatement)
            {
                AnalyseWhileStatement(whileStatement, variables);
                return;
            }
        }

        void AnalyseWhileStatement(WhileStatement whileStatement, Variables variables)
        {
            AnalyseStatements(whileStatement.Body.Statements, variables, whileStatement.Body.Locals);
        }

        void AnalyseForStatement(ForStatement forStatement, Variables variables)
        {
            Variables forVariables = new Variables(variables);
            forVariables.Add(forStatement.Variable);

            AnalyseStatements(forStatement.Body.Statements, forVariables, forStatement.Body.Locals);
        }

        void AnalyseIfStatement(IfStatement ifStatement, Variables variables)
        {
            AnalyseExpression(ifStatement.Condition, variables);
            AnalyseStatements(ifStatement.Body.Statements, variables, ifStatement.Body.Locals);

            foreach(IfStatement elseIf in ifStatement.ElseIfs)
            {
                AnalyseIfStatement(elseIf, variables);
            }

            if(ifStatement.Else != null && ifStatement.Else.Statements.Count > 0)
            {
                AnalyseStatements(ifStatement.Else.Statements, variables, ifStatement.Else.Locals);
            }
        }

        void AnalyseReturnStatement(ReturnStatement returnStatement, Variables variables)
        {
            if(returnStatement.Expression != null)
            {
                AnalyseExpression(returnStatement.Expression, variables);
            }
        }

        void AnalyseAssignmentStatement(AssignmentStatement assignmentStatement, Variables variables)
        {
            AnalyseExpression(assignmentStatement.Variable, variables);

            AnalyseExpression(assignmentStatement.Expression, variables);
        }

        void AnalyseVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement, Variables variables)
        {
            if(variableDeclarationStatement.Expression != null)
            {
                AnalyseExpression(variableDeclarationStatement.Expression, variables);
            }
        }

        void AnalyseFunctionDeclarationStatement(FunctionDeclarationStatement functionDeclarationStatement, Variables variables)
        {
            if(functionDeclarationStatement.Body != null)
            {
                Variables functionVariables = new Variables(variables);

                Variables functionLocalVariables = new Variables(functionDeclarationStatement.Body.Locals);

                foreach(var stmt in functionDeclarationStatement.Body.Statements)
                {
                    if(stmt is VariableDeclarationStatement varDeclStmt)
                    {
                        if(!functionLocalVariables.Contains(varDeclStmt.Name))
                        {
                            functionLocalVariables.Add(varDeclStmt);
                        }
                    }
                }


                foreach(var parameter in functionDeclarationStatement.Parameters)
                {
                    var paramVar = new VariableDeclarationStatement
                    {
                        Name = parameter.Name,
                        Type = parameter.Type,
                        IsParameter = true
                    };
                    functionVariables.Add(paramVar);
                }

                AnalyseStatements(functionDeclarationStatement.Body.Statements, functionVariables, functionLocalVariables);
            }
        }

        void AnalyseExpression(Expression expression, Variables variables)
        {
            if(expression is IdentifierExpression identifierExpression)
            {
                AnalyseIdentifierExpression(identifierExpression, variables);
                return;
            }

            if(expression is MemberExpression memberExpression)
            {
                AnalyseMemberExpression(memberExpression, variables);
                return;
            }
        }

        void AnalyseIdentifierExpression(IdentifierExpression identifierExpression, Variables variables)
        {
            
        }

        void AnalyseMemberExpression(MemberExpression memberExpression, Variables variables)
        {
            if(memberExpression.Parent != null)
            {
                AnalyseExpression(memberExpression.Parent, variables);
            }

            if(memberExpression.Member != null)
            {
                AnalyseExpression(memberExpression.Member, variables);
            }
        }

        bool ShouldFreeVariable(VariableDeclarationStatement variable)
        {
            if(variable.IsParameter)
                return false;

            if(VariablesBeingReturned.Contains(variable.Name))
                return false;

            if(variable.Type is IndexExpression)
                return true;

            if(variable.Expression is ArrayInitializerExpression)
                return true;

            return false;
        }

        void InsertFreesAtScopeExit(StatementList statements, Variables localVariables)
        {
            Variables variablesToFree = new Variables();

            foreach(var variable in localVariables)
            {
                if(ShouldFreeVariable(variable))
                {
                    variablesToFree.Add(variable);
                }
            }

            if(variablesToFree.Count == 0)
                return;

            InsertFreesAtAllExits(statements, variablesToFree);
        }

        void InsertFreesAtAllExits(StatementList statements, Variables variablesToFree)
        {
            if(statements.Count == 0)
                return;

            for(int i = statements.Count - 1; i >= 0; i--)
            {
                Statement statement = statements[i];

                if(statement is ReturnStatement)
                {
                    InsertFreeStatementsAt(statements, i, variablesToFree);
                    i -= variablesToFree.Count;
                }
                else if(statement is IfStatement ifStatement)
                {
                    InsertFreesInIfBranches(ifStatement, variablesToFree);
                }
            }

            if(statements.Count > 0 && !(statements[statements.Count - 1] is ReturnStatement))
            {
                InsertFreeStatementsAt(statements, statements.Count, variablesToFree);
            }
        }

        void InsertFreesInIfBranches(IfStatement ifStatement, Variables variablesToFree)
        {
            InsertFreesAtAllExits(ifStatement.Body.Statements, variablesToFree);

            foreach(var elseIf in ifStatement.ElseIfs)
            {
                InsertFreesAtAllExits(elseIf.Body.Statements, variablesToFree);
            }

            if(ifStatement.Else != null && ifStatement.Else.Statements.Count > 0)
            {
                InsertFreesAtAllExits(ifStatement.Else.Statements, variablesToFree);
            }
        }

        void InsertFreeStatementsAt(StatementList statements, int index, Variables variablesToFree)
        {
            for(int i = variablesToFree.Count - 1; i >= 0; i--)
            {
                VariableDeclarationStatement variable = variablesToFree[i];
                FreeStatement freeStatement = new FreeStatement
                {
                    Expression = new IdentifierExpression { Name = variable.Name }
                };
                statements.Insert(index, freeStatement);
            }
        }
    }
}
