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



        // -- Expressions -- //


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

        void PrintCallExpression(CallExpression callExpression, string indentation)
        {
            if(callExpression.Expression != null)
            {
                PrintExpression(callExpression.Expression, indentation);
            }
            Builder.Append("(");
            if(callExpression.Arguments != null)
            {
                PrintExpressions(callExpression.Arguments, indentation);
            }
            Builder.Append(")");
        }

        void PrintArithmeticExpression(ArithmeticExpression arithmeticExpression, string indentation)
        {
            PrintExpression(arithmeticExpression.Left, indentation);

            switch(arithmeticExpression.Operator)
            {
                case ArithmeticOperator.Addition:
                    Builder.Append(" + ");
                    break;
                case ArithmeticOperator.Subtraction:
                    Builder.Append(" - ");
                    break;
                case ArithmeticOperator.Multiplication:
                    Builder.Append(" * ");
                    break;
                case ArithmeticOperator.Division:
                    Builder.Append(" / ");
                    break;
                case ArithmeticOperator.Modulus:
                    Builder.Append(" % ");
                    break;
                case ArithmeticOperator.Exponential:
                    Builder.Append(" ^ ");
                    break;
            }

            PrintExpression(arithmeticExpression.Right, indentation);
        }

        void PrintRangeExpression(RangeExpression rangeExpression, string indentation)
        {
            PrintExpression(rangeExpression.Start, indentation);
            Builder.Append("..");
            PrintExpression(rangeExpression.End, indentation);
        }

        void PrintMemberExpression(MemberExpression memberExpression, string indentation)
        {
            PrintExpression(memberExpression.Parent, indentation);
            Builder.Append(".");
            PrintExpression(memberExpression.Member, indentation);
        }

        void PrintIndexExpression(IndexExpression indexExpression, string indentation)
        {
            PrintExpression(indexExpression.Expression, indentation);
            Builder.Append("[");
            PrintExpression(indexExpression.Index, indentation);
            Builder.Append("]");
        }

        void PrintArrayExpression(ArrayExpression arrayExpression, string indentation)
        {
            Builder.Append("{");
            if(arrayExpression.Expressions != null)
            {
                PrintExpressions(arrayExpression.Expressions, indentation);
            }
            Builder.Append("}");
        }

        void PrintUnpackExpression(UnpackExpression unpackExpression, string indentation)
        {
            PrintExpression(unpackExpression.Left, indentation);
            Builder.Append("->");
            PrintExpression(unpackExpression.Right, indentation);
        }

        void PrintRelationalExpression(RelationalExpression relationalExpression, string indentation)
        {
            PrintExpression(relationalExpression.Left, indentation);
            switch(relationalExpression.Operator)
            {
                case RelationalOperators.EqualTo:
                    Builder.Append(" == ");
                    break;
                case RelationalOperators.NotEqualTo:
                    Builder.Append(" != ");
                    break;
                case RelationalOperators.BiggerThan:
                    Builder.Append(" > ");
                    break;
                case RelationalOperators.SmallerThan:
                    Builder.Append(" < ");
                    break;
                case RelationalOperators.BiggerOrEqual:
                    Builder.Append(" >= ");
                    break;
                case RelationalOperators.SmallerOrEqual:
                    Builder.Append(" <= ");
                    break;
            }
            PrintExpression(relationalExpression.Right, indentation);
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

            if(expression is CallExpression callExpression)
            {
                PrintCallExpression(callExpression, indentation);
                return;
            }

            if(expression is ArithmeticExpression arithmeticExpression)
            {
                PrintArithmeticExpression(arithmeticExpression, indentation);
                return;
            }

            if(expression is RangeExpression rangeExpression)
            {
                PrintRangeExpression(rangeExpression, indentation);
                return;
            }

            if(expression is MemberExpression memberExpression)
            {
                PrintMemberExpression(memberExpression, indentation);
                return;
            }

            if(expression is IndexExpression indexExpression)
            {
                PrintIndexExpression(indexExpression, indentation);
                return;
            }

            if(expression is ArrayExpression arrayExpression)
            {
                PrintArrayExpression(arrayExpression, indentation);
                return;
            }

            if(expression is UnpackExpression unpackExpression)
            {
                PrintUnpackExpression(unpackExpression, indentation);
                return;
            }

            if(expression is RelationalExpression relationalExpression)
            {
                PrintRelationalExpression(relationalExpression, indentation);
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



        // -- Statements -- //


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
            Builder.Append(";");
            Builder.Append(Settings.NewLine);
        }

        void PrintClosureStatement(ClosureStatement closureStatement, string startIndentation, string indentation)
        {
            Builder.Append(startIndentation);
            Builder.Append("{");
            Builder.Append(Settings.NewLine);

            if(closureStatement.Statements != null)
            {
                PrintStatements(closureStatement.Statements, indentation + Settings.Indentation);
            }

            Builder.Append(indentation);
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

        void PrintVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement, string indentation)
        {
            Builder.Append(indentation);
            PrintExpression(variableDeclarationStatement.Type, indentation);
            Builder.Append(" ");
            Builder.Append(variableDeclarationStatement.Name);
            if(variableDeclarationStatement.Expression != null)
            {
                Builder.Append(" = ");
                PrintExpression(variableDeclarationStatement.Expression, indentation);
            }
            Builder.Append(";");
            Builder.Append(Settings.NewLine);
        }

        void PrintIfStatement(IfStatement ifStatement, string indentation)
        {
            Builder.Append(indentation);
            Builder.Append("if");
            Builder.Append(" (");
            PrintExpression(ifStatement.Condition, indentation);
            Builder.Append(")");
            Builder.Append(Settings.NewLine);
            PrintClosureStatement(ifStatement.Body, indentation, indentation);

            if(ifStatement.ElseIfStatements.Count > 0)
            {
                foreach(IfStatement elseIfStatement in ifStatement.ElseIfStatements)
                {
                    Builder.Append(indentation);
                    Builder.Append("elseif");
                    Builder.Append(" (");
                    PrintExpression(ifStatement.Condition, indentation);
                    Builder.Append(")");
                    Builder.Append(Settings.NewLine);
                    PrintClosureStatement(ifStatement.Body, indentation, indentation);
                }
            }

            if(ifStatement.ElseStatements.Statements != null && ifStatement.ElseStatements.Statements.Count > 0)
            {
                Builder.Append(indentation);
                Builder.Append("else");
                Builder.Append(Settings.NewLine);
                PrintClosureStatement(ifStatement.ElseStatements, indentation, indentation);
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
                if (statement is VariableDeclarationStatement variableDeclarationStatement)
                {
                    PrintVariableDeclarationStatement(variableDeclarationStatement, indentation);
                }
                if(statement is IfStatement ifStatement)
                {
                    PrintIfStatement(ifStatement, indentation);
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
