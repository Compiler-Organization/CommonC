using CommonC.Parser.AST.Statements;
using CommonC.Parser.AST.Expressions;
using CommonC.Semantic.Objects;
using CommonC.Parser.AST;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Semantic
{
    internal class TypeAnnotator
    {
        public void AnnotateTypes(StatementList statements)
        {
            AnnotateTypesForStatements(statements);
        }


        void AnnotateTypesForStatements(StatementList statements)
        {
            foreach (Statement statement in statements)
            {
                if (statement is FunctionDeclarationStatement functionDeclarationStatement)
                {
                    AnnotateTypeForExpression(functionDeclarationStatement.ReturnType);
                    
                    foreach(Expression parameterExpression in functionDeclarationStatement.Parameters)
                    {
                        AnnotateTypeForExpression(parameterExpression);
                    }

                    if(functionDeclarationStatement.Body != null)
                    {
                        AnnotateTypesForStatements(functionDeclarationStatement.Body.Statements);
                    }

                    continue;
                }

                if (statement is IfStatement ifStatement)
                {
                    AnnotateTypeForExpression(ifStatement.Condition);
                    AnnotateTypesForStatements(ifStatement.Body.Statements);

                    foreach(IfStatement elseIfStatement in ifStatement.ElseIfs)
                    {
                        AnnotateTypeForExpression(elseIfStatement.Condition);
                        AnnotateTypesForStatements(elseIfStatement.Body.Statements);
                    }

                    AnnotateTypesForStatements(ifStatement.Else.Statements);
                    continue;
                }

                if (statement is ForStatement forStatement)
                {

                    continue;
                }

                if (statement is WhileStatement whileStatement)
                {

                    continue;
                }

                if (statement is ClosureStatement closureStatement)
                {

                    continue;
                }
            }
        }

        void AnnotateTypeForExpression(Expression expression)
        {
            if(expression is StringExpression)
            {
                expression.TypeAnnotation = new TypeAnnotation
                {
                    IsReservedType = true,
                    ReservedType = ReservedTypes.String
                };
                return;
            }

            if(expression is NumberExpression)
            {

            }
        }
    }
}
