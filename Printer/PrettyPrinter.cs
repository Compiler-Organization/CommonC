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
            Builder.Append(booleanExpression.Value.ToString().ToLower());
        }

        void PrintIdentifierExpression(IdentifierExpression identifierExpression)
        {
            Builder.Append(identifierExpression.Name);
        }

        void PrintTypeExpression(TypeExpression typeExpression)
        {
            switch(typeExpression.Type)
            {
                case Parser.AST.ReservedTypes.I32:
                    Builder.Append("int");
                    break;

                case Parser.AST.ReservedTypes.String:
                    Builder.Append("string");
                    break;

                case Parser.AST.ReservedTypes.F64:
                    Builder.Append("double");
                    break;

                case Parser.AST.ReservedTypes.I64:
                    Builder.Append("long");
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

        void PrintParameterExpression(ParameterExpression parameterExpression, string indentation)
        {
            PrintExpression(parameterExpression.Type, indentation);
            Builder.Append(" ");
            Builder.Append(parameterExpression.Name);
            if (parameterExpression.Value != null)
            {
                Builder.Append(" = ");
                PrintExpression(parameterExpression.Value, indentation);
            }
        }

        void PrintParameterExpressions(List<ParameterExpression> parameterExpressions, string indentation)
        {
            if (parameterExpressions.Count > 0)
            {
                for (int i = 0; i < parameterExpressions.Count - 1; i++)
                {
                    PrintParameterExpression(parameterExpressions[i], indentation);
                    Builder.Append(", ");
                }
                PrintParameterExpression(parameterExpressions[parameterExpressions.Count - 1], indentation);
            }
        }

        void PrintLengthExpression(LengthExpression lengthExpression, string indentation)
        {
            Builder.Append("#");
            PrintExpression(lengthExpression.Expression, indentation);
        }

        void PrintArrayInitializerExpression(ArrayInitializerExpression arrayInitializerExpression, string indentation)
        {
            PrintIndexExpression(arrayInitializerExpression.Initializer, indentation);
            Builder.Append(" ");
            PrintArrayExpression(arrayInitializerExpression.Array, indentation);
        }

        void PrintNotExpression(NotExpression notExpression, string indentation)
        {
            Builder.Append("!");
            PrintExpression(notExpression.Expression, indentation);
        }

        void PrintNegateExpression(NegateExpression negateExpression, string indentation)
        {
            Builder.Append("-");
            PrintExpression(negateExpression.Expression, indentation);
        }

        void PrintParenthesizedExpression(ParenthesizedExpression parenthesizedExpression, string indentation)
        {
            Builder.Append("(");
            PrintExpression(parenthesizedExpression.Expression, indentation);
            Builder.Append(")");
        }

        void PrintObjectInitializerExpression(ObjectInitializerExpression objectInitializerExpression, string indentation)
        {
            PrintExpression(objectInitializerExpression.Expression, indentation);
            Builder.Append(" {");
            Builder.Append(Settings.NewLine);
            foreach (AssignmentStatement assignmentStatement in objectInitializerExpression.PropertyAssignments)
            {
                Builder.Append(indentation + Settings.Indentation);
                PrintExpression(assignmentStatement.Variable, indentation);
                Builder.Append(": ");
                PrintExpression(assignmentStatement.Expression!, indentation);
                
                if (objectInitializerExpression.PropertyAssignments.IndexOf(assignmentStatement) != objectInitializerExpression.PropertyAssignments.Count - 1)
                {
                    Builder.Append("," + Settings.NewLine);
                }
            }
            Builder.Append(Settings.NewLine);
            Builder.Append(indentation);
            Builder.Append("}");
        }

        void PrintExpression(Expression expression, string indentation)
        {
            if(expression is ObjectInitializerExpression objectInitializerExpression)
            {
                PrintObjectInitializerExpression(objectInitializerExpression, indentation);
            }

            if(expression is ParenthesizedExpression parenthesizedExpression)
            {
                PrintParenthesizedExpression(parenthesizedExpression, indentation);
            }

            if(expression is NotExpression notExpression)
            {
                PrintNotExpression(notExpression, indentation);
            }

            if(expression is NegateExpression negateExpression)
            {
                PrintNegateExpression(negateExpression, indentation);
            }

            if(expression is ArrayInitializerExpression arrayInitializerExpression)
            {
                PrintArrayInitializerExpression(arrayInitializerExpression, indentation);
            }

            if(expression is LengthExpression lengthExpression)
            {
                PrintLengthExpression(lengthExpression, indentation);
            }

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
            if(functionDeclarationStatement.Parameters != null && functionDeclarationStatement.Parameters.Count > 0)
            {
                Builder.Append("(");
                PrintParameterExpressions(functionDeclarationStatement.Parameters, indentation);
                Builder.Append(")");
            }

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

            if(ifStatement.ElseIfs.Count > 0)
            {
                foreach(IfStatement elseIfStatement in ifStatement.ElseIfs)
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

            if(ifStatement.Else.Statements != null && ifStatement.Else.Statements.Count > 0)
            {
                Builder.Append(indentation);
                Builder.Append("else");
                Builder.Append(Settings.NewLine);
                PrintClosureStatement(ifStatement.Else, indentation, indentation);
            }
        }

        void PrintForStatement(ForStatement forStatement, string indentation)
        {
            Builder.Append(indentation);
            Builder.Append("for ");
            PrintExpression(forStatement.Range, indentation);
            Builder.Append(", ");
            Builder.Append(forStatement.Variable.Name);
            Builder.Append(Settings.NewLine);

            PrintClosureStatement(forStatement.Body, indentation, indentation);
        }

        void PrintReturnStatement(ReturnStatement returnStatement, string indentation)
        {
            Builder.Append(indentation);
            Builder.Append("return");
            if(returnStatement.Expression != null)
            {
                Builder.Append(" ");
                PrintExpression(returnStatement.Expression, indentation);
            }
            Builder.Append(";");
            Builder.Append(Settings.NewLine);
        }

        void PrintWhileStatement(WhileStatement whileStatement, string indentation)
        {
            Builder.Append(indentation);
            Builder.Append("while ");
            PrintExpression(whileStatement.Expression, indentation);
            Builder.Append(Settings.NewLine);
            PrintClosureStatement(whileStatement.Body, indentation, indentation);
        }

        void PrintStructStatement(StructStatement structStatement, string indentation)
        {
            Builder.Append(indentation);
            Builder.Append("struct ");
            Builder.Append(structStatement.Name);
            Builder.Append(" {");
            Builder.Append(Settings.NewLine);
            foreach (VariableDeclarationStatement variableDeclarationStatement in structStatement.Fields)
            {
                Builder.Append(indentation + Settings.Indentation);
                PrintExpression(variableDeclarationStatement.Type, indentation);
                Builder.Append(" ");
                Builder.Append(variableDeclarationStatement.Name);
                if (variableDeclarationStatement.Expression != null)
                {
                    Builder.Append(" = ");
                    PrintExpression(variableDeclarationStatement.Expression, indentation);
                }
                if(structStatement.Fields.IndexOf(variableDeclarationStatement) != structStatement.Fields.Count - 1)
                {
                    Builder.Append(",");
                }
                Builder.Append(Settings.NewLine);
            }
            Builder.Append("}");
            Builder.Append(Settings.NewLine);
        }

        void PrintStatements(StatementList statements, string indentation)
        {
            foreach(Statement statement in statements)
            {
                if(statement is StructStatement structStatement)
                {
                    PrintStructStatement(structStatement, indentation);
                }
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
                if(statement is ForStatement forStatement)
                {
                    PrintForStatement(forStatement, indentation);
                }
                if(statement is ReturnStatement returnStatement)
                {
                    PrintReturnStatement(returnStatement, indentation);
                }
                if(statement is WhileStatement whileStatement)
                {
                    PrintWhileStatement(whileStatement, indentation);
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
