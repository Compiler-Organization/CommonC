using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Printer
{
    public class PrettyPrinter
    {
        StatementList Statements { get; set; }
        StringBuilder Builder { get; set; }
        PrettyPrinterSettings Settings { get; set; }

        public PrettyPrinter(StatementList statementList, PrettyPrinterSettings settings)
        {
            Statements = statementList;
            Builder = new StringBuilder();
            Settings = settings;
        }

        void PrintStringExpression(StringExpression stringExpression)
        {
            Builder.Append($"\"{stringExpression.Value}\"");
        }

        void PrintNumberExpression(NumberExpression numberExpression)
        {
            Builder.Append(numberExpression.Value.ToString());
        }

        void PrintBooleanExpression(BooleanExpression booleanExpression)
        {
            Builder.Append(booleanExpression.Value.ToString());
        }

        void PrintIdentifierExpression(IdentifierExpression identifierExpression)
        {
            Builder.Append(identifierExpression.Name);
        }

        void PrintTypeExpression(TypeExpression typeExpression)
        {
            switch(typeExpression.Type)
            {
                case Parser.AST.ReservedTypes.Int:
                    Builder.Append("int");
                    break;

                case Parser.AST.ReservedTypes.String:
                    Builder.Append("str");
                    break;

                case Parser.AST.ReservedTypes.Bool:
                    Builder.Append("bool");
                    break;
            } 
        }


        void PrintExpression(Expression expression, string indentation)
        {
            if(expression is StringExpression stringExpression)
            {
                PrintStringExpression(stringExpression);
                return;
            }

            if(expression is NumberExpression numberExpression)
            {
                PrintNumberExpression(numberExpression);
                return;
            }

            if(expression is BooleanExpression booleanExpression)
            {
                PrintBooleanExpression(booleanExpression);
                return;
            }

            if(expression is IdentifierExpression identifierExpression)
            {
                PrintIdentifierExpression(identifierExpression);
                return;
            }

            if(expression is TypeExpression typeExpression)
            {
                PrintTypeExpression(typeExpression);
                return;
            }
        }

        void PrintExpressions(ExpressionList expressions, string indentation)
        {
            if(expressions.Count > 0)
            {
                for(int i = 0; i < expressions.Count - 1; i++)
                {
                    PrintExpression(expressions[i], indentation);
                    Builder.Append(", ");
                }

                PrintExpression(expressions[expressions.Count - 1], indentation);
            }
        }

        void PrintCallStatement(CallStatement callStatement, string indentation)
        {
            Builder.Append(indentation);

            if(callStatement.Expression != null)
            {
                PrintExpression(callStatement.Expression, indentation);
            }

            Builder.Append("(");

            if(callStatement.Arguments != null)
            {
                PrintExpressions(callStatement.Arguments, indentation);
            }

            Builder.Append(")");
            Builder.Append(Settings.NewLine);
        }

        void PrintClosureStatement(ClosureStatement closureStatement, string startIndentation, string indentation)
        {
            Builder.Append(startIndentation);
            Builder.Append("{");
            Builder.Append(Settings.NewLine);
            PrintStatements(closureStatement.Statements, indentation + Settings.Indentation);
            Builder.Append("}");
            Builder.Append(Settings.NewLine);
        }

        void PrintFunctionDeclarationStatement(FunctionDeclarationStatement functionDeclarationStatement, string indentation)
        {
            Builder.Append(indentation);
            PrintExpression(functionDeclarationStatement.ReturnType, indentation);
            Builder.Append(" ");
            Builder.Append(functionDeclarationStatement.Name);
            Builder.Append("(");
            if(functionDeclarationStatement.Parameters != null)
            {
                PrintExpressions(functionDeclarationStatement.Parameters, indentation);
            }
            Builder.Append(")");

            if(functionDeclarationStatement.Body != null)
            {
                PrintClosureStatement(functionDeclarationStatement.Body, " ", indentation);
            }
        }

        void PrintStatements(StatementList statements, string indentation)
        {
            foreach(Statement statement in statements)
            {
                if(statement is CallStatement callStatement)
                {
                    PrintCallStatement(callStatement, indentation);
                }
                if(statement is ClosureStatement closureStatement)
                {
                    PrintClosureStatement(closureStatement, indentation, indentation);
                }
                if(statement is FunctionDeclarationStatement functionDeclarationStatement)
                {
                    PrintFunctionDeclarationStatement(functionDeclarationStatement, indentation);
                }
            }
        }

        public string Print()
        {
            PrintStatements(Statements, "");

            return Builder.ToString();
        }
    }
}
