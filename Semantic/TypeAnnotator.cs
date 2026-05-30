using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using CommonC.Semantic.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Semantic
{
    internal class TypeAnnotator
    {
        Dictionary<string, StructStatement> Structs = new Dictionary<string, StructStatement>();
        Dictionary<string, FunctionDeclarationStatement> Functions = new Dictionary<string, FunctionDeclarationStatement>();

        TypeAnnotation ResolveTypeFromExpression(Expression expression, Variables? variables)
        {
            if (expression is StringExpression)
            {
                return expression.TypeAnnotation = new TypeAnnotation
                {
                    IsReservedType = true,
                    ReservedType = ReservedTypes.String
                };
            }
            if (expression is NumberExpression numberExpression)
            {
                if (numberExpression.IsDouble)
                {
                    return expression.TypeAnnotation = new TypeAnnotation
                    {
                        IsReservedType = true,
                        ReservedType = ReservedTypes.F64
                    };
                }
                else
                {
                    return expression.TypeAnnotation = new TypeAnnotation
                    {
                        IsReservedType = true,
                        ReservedType = ReservedTypes.I32
                    };
                }
            }
            if (expression is TypeExpression typeExpression)
            {
                return expression.TypeAnnotation = new TypeAnnotation
                {
                    IsReservedType = true,
                    ReservedType = typeExpression.Type
                };
            }
            if (expression is IdentifierExpression identifierExpression)
            {
                if (Structs.ContainsKey(identifierExpression.Name))
                {
                    return new TypeAnnotation
                    {
                        IsStruct = true,
                        Struct = Structs[identifierExpression.Name]
                    };
                }

                return expression.TypeAnnotation = ResolveTypeFromExpression(variables.GetVariable(identifierExpression.Name).Type, variables);
            }
            if (expression is CallExpression callExpression)
            {
                return expression.TypeAnnotation = ResolveTypeFromExpression(callExpression.Expression, variables);
                //if (callExpression.Expression is IdentifierExpression callIdentifierExpression)
                //{
                //    if (Functions.ContainsKey(callIdentifierExpression.Name))
                //    {
                //        return ResolveTypeFromExpression(Functions[callIdentifierExpression.Name].ReturnType, variables);
                //    }
                //}

                //throw new Exception($"Call expression of type {callExpression.Expression.GetType().Name} is not supported when resolving types from expressions.");
            }
            if (expression is IndexExpression indexExpression)
            {
                if(indexExpression.Index != null)
                    ResolveTypeFromExpression(indexExpression.Index, variables);

                TypeAnnotation indexTypeAnnotation = ResolveTypeFromExpression(indexExpression.Expression, variables);
                indexTypeAnnotation.IsArray = true;
                return indexTypeAnnotation;
            }
            if (expression is NotExpression notExpression)
            {
                return expression.TypeAnnotation = ResolveTypeFromExpression(notExpression.Expression, variables);
            }
            if (expression is BooleanExpression booleanExpression)
            {
                return expression.TypeAnnotation = new TypeAnnotation
                {
                    IsReservedType = true,
                    ReservedType = ReservedTypes.Bool
                };
            }
            if (expression is ParenthesizedExpression parenthesizedExpression)
            {
                return expression.TypeAnnotation = ResolveTypeFromExpression(parenthesizedExpression.Expression, variables);
            }
            if (expression is RelationalExpression relationalExpression)
            {
                ResolveTypeFromExpression(relationalExpression.Right, variables);
                return expression.TypeAnnotation = ResolveTypeFromExpression(relationalExpression.Left, variables);
            }
            if (expression is ArithmeticExpression arithmeticExpression)
            {
                ResolveTypeFromExpression(arithmeticExpression.Right, variables);
                return expression.TypeAnnotation = ResolveTypeFromExpression(arithmeticExpression.Left, variables);
            }
            if (expression is ArrayInitializerExpression arrayInitializerExpression)
            {
                ResolveTypeFromExpression(arrayInitializerExpression.Array, variables);
                return expression.TypeAnnotation = ResolveTypeFromExpression(arrayInitializerExpression.Index, variables);
            }
            if(expression is ArrayExpression arrayExpression)
            {
                foreach(Expression element in arrayExpression.Expressions)
                {
                    element.TypeAnnotation = ResolveTypeFromExpression(element, variables);
                }

                if(arrayExpression.Expressions.Any())
                {
                    return expression.TypeAnnotation = arrayExpression.Expressions.First().TypeAnnotation;
                }
                else
                {
                    return expression.TypeAnnotation = new TypeAnnotation
                    {
                        IsArray = true,
                    };
                }
            }
            if (expression is SizeOfExpression sizeOfExpression)
            {
                return expression.TypeAnnotation = ResolveTypeFromExpression(sizeOfExpression.Expression, variables);
            }
            if (expression is LengthExpression lengthExpression)
            {
                return expression.TypeAnnotation = ResolveTypeFromExpression(lengthExpression.Expression, variables);
            }
            if(expression is ParameterExpression parameterExpression)
            {
                return expression.TypeAnnotation = ResolveTypeFromExpression(parameterExpression.Type, variables);
            }
            if(expression is RangeExpression rangeExpression)
            {
                ResolveTypeFromExpression(rangeExpression.End, variables);
                return expression.TypeAnnotation = ResolveTypeFromExpression(rangeExpression.Start, variables);
            }
            if (expression is MemberExpression memberExpression)
            {
                ExpressionList memberChain = memberExpression.Flatten();
                StructStatement? currentStruct = null;

                if (memberChain.Count <= 0)
                {
                    throw new Exception("Invalid member expression when solving type, member chain contained 0 members!");
                }

                if (memberChain.First() is IdentifierExpression firstIdentifier)
                {
                    if (variables == null)
                    {
                        throw new Exception($"Variables cannot be null when resolving type from member expression with identifier {firstIdentifier.Name} as first member.");
                    }

                    VariableDeclarationStatement parentVariable = variables.GetVariable(firstIdentifier.Name);
                    IdentifierExpression? innerIdentifier = GetInnerIdentifierExpression(parentVariable.Type);
                    if (innerIdentifier == null)
                    {
                        throw new Exception($"Could not resolve inner identifier expression for variable {firstIdentifier.Name}.");
                    }

                    currentStruct = Structs[innerIdentifier.Name];
                }

                if (currentStruct == null)
                {
                    throw new Exception($"Could not resolve struct for member expression when resolving type.");
                }

                foreach (Expression member in memberChain.Skip(1))
                {
                    if (member is IdentifierExpression memberIdentifier)
                    {
                        if (Structs.ContainsKey(memberIdentifier.Name))
                        {
                            currentStruct = Structs[memberIdentifier.Name];
                            continue;
                        }

                        VariableDeclarationStatement field = currentStruct.GetField(memberIdentifier.Name);
                        return expression.TypeAnnotation = ResolveTypeFromExpression(field.Type, variables); // may cause issues as variables include all variables in the current scope, including parameters and local variables.
                    }
                }
            }

            throw new Exception($"Could not resolve type of expression with type {expression.GetType().Name}");
        }

        IdentifierExpression? GetInnerIdentifierExpression(Expression expression)
        {
            return expression switch
            {
                CallExpression expr => GetInnerIdentifierExpression(expr.Expression),
                IndexExpression expr => GetInnerIdentifierExpression(expr.Expression),
                ArithmeticExpression expr => GetInnerIdentifierExpression(expr.Left),
                RelationalExpression expr => GetInnerIdentifierExpression(expr.Left),
                ArrayExpression expr => GetInnerIdentifierExpression(expr.Expressions.Any() ? expr.Expressions.First() : throw new Exception($"Cannot resolve inner identifier expression of empty array")),
                LengthExpression expr => GetInnerIdentifierExpression(expr.Expression),
                MemberExpression expr => GetInnerIdentifierExpression(expr.Parent),
                NegateExpression expr => GetInnerIdentifierExpression(expr.Expression),
                NotExpression expr => GetInnerIdentifierExpression(expr.Expression),
                ObjectInitializerExpression expr => GetInnerIdentifierExpression(expr.Expression),
                ParameterExpression expr => GetInnerIdentifierExpression(expr.Type),
                ParenthesizedExpression expr => GetInnerIdentifierExpression(expr.Expression),
                SizeOfExpression expr => GetInnerIdentifierExpression(expr.Expression),
                UnpackExpression expr => GetInnerIdentifierExpression(expr.Left),
                TypeExpression => null,
                IdentifierExpression expr => expr,
                _ => throw new Exception($"Inner identifier expression of type {expression.GetType().Name} is not supported.")
            };
        }

        public void AnnotateTypes(ClosureStatement closure)
        {
            List<StructStatement> structs = closure.Statements.OfType<StructStatement>().ToList();
            foreach (StructStatement structStatement in structs)
            {
                Structs.Add(structStatement.Name, structStatement);
            }

            List<FunctionDeclarationStatement> functionDeclarationStatements = closure.Statements.OfType<FunctionDeclarationStatement>().ToList();
            foreach (FunctionDeclarationStatement functionDeclarationStatement in functionDeclarationStatements)
            {
                Functions.Add(functionDeclarationStatement.Name, functionDeclarationStatement);
            }

            AnnotateStatements(closure);
        }

        void AnnotateTypeForParameters(List<ParameterExpression> parameters, Variables variables)
        {
            foreach (ParameterExpression parameter in parameters)
            {
                AnnotateTypeForExpression(parameter, variables);
            }
        }

        void AnnotateTypeForExpression(Expression expression, Variables variables)
        {
            expression.TypeAnnotation = ResolveTypeFromExpression(expression, variables);
        }

        void AnnotateTypeForExpressions(List<Expression> expressions, Variables variables)
        {
            foreach (Expression expression in expressions)
            {
                AnnotateTypeForExpression(expression, variables);
            }
        }

        void AnnotateStatements(ClosureStatement closure)
        {
            foreach (Statement statement in closure.Statements)
            {
                if (statement is FunctionDeclarationStatement functionDeclarationStatement)
                {
                    AnnotateTypeForExpression(functionDeclarationStatement.ReturnType, closure.Locals);
                    AnnotateTypeForParameters(functionDeclarationStatement.Parameters, closure.Locals);

                    if (functionDeclarationStatement.Body != null)
                        AnnotateStatements(functionDeclarationStatement.Body);

                    continue;
                }
                if (statement is IfStatement ifStatement)
                {
                    AnnotateTypeForExpression(ifStatement.Condition, closure.Locals);
                    AnnotateStatements(ifStatement.Body);

                    foreach (IfStatement elseIfStatement in ifStatement.ElseIfs)
                    {
                        AnnotateTypeForExpression(elseIfStatement.Condition, closure.Locals);
                        AnnotateStatements(elseIfStatement.Body);
                    }

                    AnnotateStatements(ifStatement.Else);
                    continue;
                }
                if (statement is ForStatement forStatement)
                {
                    AnnotateTypeForExpression(forStatement.Range, closure.Locals);
                    AnnotateTypeForExpression(forStatement.Variable.Type, closure.Locals);
                    AnnotateStatements(forStatement.Body);
                    continue;
                }
                if (statement is VariableDeclarationStatement variableDeclarationStatement)
                {
                    AnnotateTypeForExpression(variableDeclarationStatement.Type, closure.Locals);
                    variableDeclarationStatement.TypeAnnotation = variableDeclarationStatement.Type.TypeAnnotation;
                    continue;
                }
                if (statement is AssignmentStatement assignmentStatement)
                {
                    AnnotateTypeForExpression(assignmentStatement.Variable, closure.Locals);
                    AnnotateTypeForExpression(assignmentStatement.Expression, closure.Locals);
                    continue;
                }
                if (statement is CallStatement callStatement)
                {
                    // AnnotateTypeForExpression(callStatement.Expression, closure.Locals);
                    AnnotateTypeForExpressions(callStatement.Arguments, closure.Locals);
                    continue;
                }
                if (statement is ClosureStatement closureStatement)
                {
                    AnnotateStatements(closureStatement);
                    continue;
                }
                if (statement is StructStatement structStatement)
                {
                    foreach (VariableDeclarationStatement field in structStatement.Fields)
                    {
                        AnnotateTypeForExpression(field.Type, closure.Locals);
                        field.TypeAnnotation = field.Type.TypeAnnotation;
                    }
                    continue;
                }
                if (statement is ReturnStatement returnStatement)
                {
                    if (returnStatement.Expression != null)
                        AnnotateTypeForExpression(returnStatement.Expression, closure.Locals);

                    continue;
                }
                if (statement is WhileStatement whileStatement)
                {
                    AnnotateTypeForExpression(whileStatement.Expression, closure.Locals);
                    AnnotateStatements(whileStatement.Body);
                    continue;
                }
            }
        }
    }
}
