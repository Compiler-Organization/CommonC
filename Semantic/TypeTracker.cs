using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using CommonC.Semantic.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Semantic
{
    internal class TypeTracker
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
            if(expression is CharacterExpression)
            {
                return expression.TypeAnnotation = new TypeAnnotation
                {
                    IsReservedType = true,
                    ReservedType = ReservedTypes.Char
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
                    return expression.TypeAnnotation = new TypeAnnotation
                    {
                        IsStruct = true,
                        Struct = Structs[identifierExpression.Name]
                    };
                }

                if(Functions.ContainsKey(identifierExpression.Name))
                {
                    return expression.TypeAnnotation = ResolveTypeFromExpression(Functions[identifierExpression.Name].ReturnType, variables);
                }

                TypeAnnotation variableAnnotation = ResolveTypeFromExpression(variables.GetVariable(identifierExpression.Name).Type, variables);
                variableAnnotation.IsVariable = true;
                return expression.TypeAnnotation = variableAnnotation;
            }
            if (expression is CallExpression callExpression)
            {
                if(callExpression.Arguments != null && callExpression.Arguments.Count > 0)
                {
                    foreach (Expression argument in callExpression.Arguments)
                    {
                        argument.TypeAnnotation = ResolveTypeFromExpression(argument, variables);
                    }
                }

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

                TypeAnnotation indexTypeAnnotation = ResolveTypeFromExpression(indexExpression.Expression, variables).Copy();

                indexTypeAnnotation.IsArray = true;
                indexTypeAnnotation.ArrayDepth += indexTypeAnnotation.IsVariable ? 0 : 1;

                return indexExpression.TypeAnnotation = indexTypeAnnotation;
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
                    TypeAnnotation elementType = arrayExpression.Expressions.First().TypeAnnotation.Copy();
                    elementType.IsArray = true;
                    return expression.TypeAnnotation = elementType;
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
            if(expression is ObjectInitializerExpression objectInitializerExpression)
            {
                if(objectInitializerExpression.Expression is IdentifierExpression identifierExpr)
                {
                    ResolveTypeFromExpression(objectInitializerExpression.Expression, variables);
                    if (Structs.ContainsKey(identifierExpr.Name))
                    {
                        StructStatement structStatement = Structs[identifierExpr.Name];
                        foreach (AssignmentStatement propertyAssignment in objectInitializerExpression.Fields)
                        {
                            if (propertyAssignment.Variable is IdentifierExpression propertyIdentifier)
                            {
                                VariableDeclarationStatement field = structStatement.GetField(propertyIdentifier.Name);
                                propertyAssignment.Variable.TypeAnnotation = field.TypeAnnotation;
                                TrackTypeForExpression(propertyAssignment.Expression, variables);
                                if (!propertyAssignment.Expression.TypeAnnotation.Match(field.TypeAnnotation))
                                {
                                    throw new Exception($"Type of property assignment for property {propertyIdentifier.Name} ({propertyAssignment.Expression.TypeAnnotation.ToString()}) does not match type of field in struct {structStatement.Name} ({field.TypeAnnotation.ToString()}).");
                                }
                            }
                            else
                            {
                                throw new Exception($"Property assignment variable is not an identifier expression.");
                            }
                        }
                        return objectInitializerExpression.TypeAnnotation = new TypeAnnotation
                        {
                            IsStruct = true,
                            Struct = structStatement
                        };
                    }
                    else
                    {
                        throw new Exception($"Struct {identifierExpr.Name} not found when resolving type from object initializer expression.");
                    }
                }

                throw new Exception($"Expression of type {objectInitializerExpression.Expression.GetType().Name} is not supported as the expression of an object initializer expression when resolving types.");
            }
            if (expression is MemberExpression memberExpression)
            {
                ExpressionList memberChain = memberExpression.Flatten();
                StructStatement? currentStruct = null;

                if (memberChain.Count <= 0)
                {
                    throw new Exception("Invalid member expression when solving type, member chain contained 0 members!");
                }

                memberChain.First().TypeAnnotation = ResolveTypeFromExpression(memberChain.First(), variables);

                if(!memberChain.First().TypeAnnotation.IsStruct)
                {
                    throw new Exception($"First member of member expression must be of struct type when resolving type from member expression, but was of type {memberChain.First().TypeAnnotation}");
                }

                currentStruct = memberChain.First().TypeAnnotation.Struct;

                memberChain.First().TypeAnnotation = new TypeAnnotation
                {
                    IsStruct = true,
                    Struct = currentStruct
                };

                foreach (Expression member in memberChain.Skip(1))
                {
                    IdentifierExpression? memberIdentifier = GetInnerIdentifierExpression(member);

                    if(memberIdentifier == null)
                    {
                        throw new Exception($"Could not resolve inner identifier expression for member in member expression when resolving type from member expression.");
                    }

                    Expression fieldType = currentStruct.Fields.GetVariable(memberIdentifier.Name).Type;

                    if (fieldType is IdentifierExpression or IndexExpression)
                    {
                        VariableDeclarationStatement field = currentStruct.GetField(memberIdentifier.Name);
                        ResolveTypeFromExpression(member, [field, .. variables]);
                        member.TypeAnnotation = ResolveTypeFromExpression(field.Type, variables);

                        if (memberChain.IsLast(member))
                        {
                            return expression.TypeAnnotation = ResolveTypeFromExpression(field.Type, variables);
                        }

                        IdentifierExpression? fieldTypeIdentifier = GetInnerIdentifierExpression(field.Type);
                        if (fieldTypeIdentifier == null)
                        {
                            throw new Exception($"Could not resolve inner identifier expression for field {field.Name} of struct {currentStruct.Name}: {field.Type.TypeAnnotation.ToString()}");
                        }

                        if (Structs.ContainsKey(fieldTypeIdentifier.Name))
                        {
                            currentStruct = Structs[fieldTypeIdentifier.Name];
                            continue;
                        }
                        else
                        {
                            throw new Exception($"Struct {fieldTypeIdentifier.Name} not found when resolving type from member expression.");
                        }
                    }

                    if (fieldType is TypeExpression fieldTypeExpression)
                    {
                        if (!memberChain.IsLast(member))
                        {
                            throw new Exception($"Member {memberIdentifier.Name} of struct {currentStruct.Name} is of reserved type {fieldTypeExpression.Type}, but is not the last member in the member expression chain.");
                        }
                        member.TypeAnnotation = ResolveTypeFromExpression(fieldTypeExpression, variables);
                        return expression.TypeAnnotation = member.TypeAnnotation;
                    }

                    throw new Exception($"Member expressions accessing field of type {currentStruct.Fields.GetVariable(memberIdentifier.Name).Type} are not supported.");
                }
            }
            if(expression is NegateExpression negateExpression)
            {
                return expression.TypeAnnotation = ResolveTypeFromExpression(negateExpression.Expression, variables);
            }
            if(expression is NullExpression)
            {
                return expression.TypeAnnotation = new TypeAnnotation
                {
                    IsReservedType = true,
                    ReservedType = ReservedTypes.Null
                };
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

        public void TrackTypes(ClosureStatement closure)
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

            TrackStatements(closure);
        }

        void TrackTypeForParameters(ParameterExpressionList parameters, Variables variables)
        {
            foreach (ParameterExpression parameter in parameters)
            {
                TrackTypeForExpression(parameter, variables);
            }
        }

        void TrackTypeForExpression(Expression expression, Variables variables)
        {
            expression.TypeAnnotation = ResolveTypeFromExpression(expression, variables);
        }

        void TrackTypeForExpressions(List<Expression> expressions, Variables variables)
        {
            foreach (Expression expression in expressions)
            {
                TrackTypeForExpression(expression, variables);
            }
        }

        void TrackStatements(ClosureStatement closure)
        {
            foreach (Statement statement in closure.Statements)
            {
                TrackStatement(statement, closure.Locals);
            }
        }

        void TrackStatement(Statement statement, Variables variables)
        {
            if (statement is FunctionDeclarationStatement functionDeclarationStatement)
            {
                TrackTypeForExpression(functionDeclarationStatement.ReturnType, variables);
                TrackTypeForParameters(functionDeclarationStatement.Parameters, variables);

                if (functionDeclarationStatement.Body != null)
                    TrackStatements(functionDeclarationStatement.Body);

                return;
            }
            if (statement is IfStatement ifStatement)
            {
                TrackTypeForExpression(ifStatement.Condition, variables);
                TrackStatements(ifStatement.Body);

                foreach (IfStatement elseIfStatement in ifStatement.ElseIfs)
                {
                    TrackTypeForExpression(elseIfStatement.Condition, variables);
                    TrackStatements(elseIfStatement.Body);
                }

                TrackStatements(ifStatement.Else);
                return;
            }
            if (statement is ForStatement forStatement)
            {
                TrackTypeForExpression(forStatement.Range, variables);
                TrackTypeForExpression(forStatement.Variable.Type, variables);
                TrackStatements(forStatement.Body);
                return;
            }
            if (statement is VariableDeclarationStatement variableDeclarationStatement)
            {
                TrackTypeForExpression(variableDeclarationStatement.Type, variables);

                if (variableDeclarationStatement.Expression != null)
                    TrackTypeForExpression(variableDeclarationStatement.Expression, variables);

                variableDeclarationStatement.TypeAnnotation = variableDeclarationStatement.Type.TypeAnnotation;
                return;
            }
            if (statement is AssignmentStatement assignmentStatement)
            {
                TrackTypeForExpression(assignmentStatement.Variable, variables);
                TrackTypeForExpression(assignmentStatement.Expression, variables);
                return;
            }
            if (statement is CallStatement callStatement)
            {
                TrackTypeForExpression(callStatement.Expression, variables);

                if(callStatement.Arguments != null && callStatement.Arguments.Count > 0)
                    TrackTypeForExpressions(callStatement.Arguments, variables);

                return;
            }
            if (statement is ClosureStatement closureStatement)
            {
                TrackStatements(closureStatement);
                return;
            }
            if (statement is StructStatement structStatement)
            {
                foreach (VariableDeclarationStatement field in structStatement.Fields)
                {
                    TrackTypeForExpression(field.Type, variables);
                    field.TypeAnnotation = field.Type.TypeAnnotation;
                }
                return;
            }
            if (statement is ReturnStatement returnStatement)
            {
                if (returnStatement.Expression != null)
                    TrackTypeForExpression(returnStatement.Expression, variables);

                return;
            }
            if (statement is WhileStatement whileStatement)
            {
                TrackTypeForExpression(whileStatement.Expression, variables);
                TrackStatements(whileStatement.Body);
                return;
            }
        }
    }
}
