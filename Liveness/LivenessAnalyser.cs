using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Liveness
{
    /*
    The idea for this will be an analyser that goes through every read / write to a variable / what would be memory, then insert a free at the last read/write to the memory location.
    * Variables that returns with a functions needs to be preserved.
    * Need to take into account variables can be assigned conditionally. E.g "if var == true, bigVar = ...".

    */
    public class LivenessAnalyser
    {
        StatementList Statements { get; set; }

        public LivenessAnalyser(StatementList statements)
        {
            Statements = statements;
        }

        public void Analyse()
        {
            AnalyseStatements(Statements);
        }

        void AnalyseStatements(StatementList statements)
        {
            foreach(Statement statement in statements)
            {
                AnalyseStatement(statement);
            }
        }

        void AnalyseStatement(Statement statement)
        {
            if(statement is FunctionDeclarationStatement functionDeclarationStatement)
            {
                AnalyseFunctionDeclarationStatement(functionDeclarationStatement);
                return;
            }

            if(statement is VariableDeclarationStatement variableDeclarationStatement)
            {
                AnalyseVariableDeclarationStatement(variableDeclarationStatement);
                return;
            }

            if(statement is AssignmentStatement assignmentStatement)
            {
                AnalyseAssignmentStatement(assignmentStatement);
                return;
            }

            if(statement is ReturnStatement returnStatement)
            {
                AnalyseReturnStatement(returnStatement);
                return;
            }

            if(statement is IfStatement ifStatement)
            {
                AnalyseIfStatement(ifStatement);
                return;
            }

            if(statement is ForStatement forStatement)
            {
                AnalyseForStatement(forStatement);
                return;
            }

            if(statement is WhileStatement whileStatement)
            {
                AnalyseWhileStatement(whileStatement);
                return;
            }
        }

        void AnalyseWhileStatement(WhileStatement whileStatement)
        {

        }

        void AnalyseForStatement(ForStatement forStatement)
        {

        }

        void AnalyseIfStatement(IfStatement ifStatement)
        {

        }

        void AnalyseReturnStatement(ReturnStatement returnStatement)
        {

        }

        void AnalyseAssignmentStatement(AssignmentStatement assignmentStatement)
        {

        }
        void AnalyseVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement)
        {

        }

        void AnalyseFunctionDeclarationStatement(FunctionDeclarationStatement functionDeclarationStatement)
        {

        }

        void AnalyseExpression(Expression expression)
        {
            if(expression is IdentifierExpression identifierExpression)
            {
                AnalyseIdentifierExpression(identifierExpression);
                return;
            }

            if(expression is MemberExpression memberExpression)
            {
                AnalyseMemberExpression(memberExpression);
                return;
            }
        }

        void AnalyseIdentifierExpression(IdentifierExpression identifierExpression)
        {

        }

        void AnalyseMemberExpression(MemberExpression memberExpression)
        {

        }
    }
}
