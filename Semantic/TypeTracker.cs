using CommonC.Error;
using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using CommonC.Semantic.Objects;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace CommonC.Semantic
{
    internal class TypeTracker
    {
        Dictionary<string, StructStatement> Structs = new Dictionary<string, StructStatement>();
    
        Dictionary<string, ClassStatement> Classes = new Dictionary<string, ClassStatement>();

        Dictionary<string, EnumStatement> Enums = new Dictionary<string, EnumStatement>();

        Functions Functions = new Functions();

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
                string name = identifierExpression.Name;

                if (Structs.TryGetValue(name, out var structDeclaration))
                {
                    return expression.TypeAnnotation = new TypeAnnotation
                    {
                        IsStruct = true,
                        Struct = structDeclaration
                    };
                }

                if (Classes.TryGetValue(name, out var classStatement))
                {
                    return expression.TypeAnnotation = new TypeAnnotation
                    {
                        IsClass = true,
                        Class = classStatement
                    };
                }

                if (Enums.TryGetValue(name, out var enumStatement))
                {
                    return expression.TypeAnnotation = enumStatement.TypeAnnotation;
                }

                if (variables?.Contains(name) == true)
                {
                    var variable = variables.GetVariable(name);
                    var variableAnnotation = ResolveTypeFromExpression(variable.Type, variables);

                    variableAnnotation.IsVariable = true;
                    return expression.TypeAnnotation = variableAnnotation;
                }

                List<FunctionDeclarationStatement> matchingFunctions = Functions.Where(f => f.Name == name).ToList();

                if (matchingFunctions.Count > 0)
                {
                    FunctionDeclarationStatement function = matchingFunctions[0];
                    return expression.TypeAnnotation = ResolveTypeFromExpression(function.ReturnType, variables);
                }

                throw ErrorHandler.CreateError($"'{name}' does not exist in the current context.", identifierExpression);
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

                //throw ErrorHandler.CreateError($"Call expression of type {callExpression.Expression.GetType().Name} is not supported when resolving types from expressions.");
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
            if(expression is LogicalExpression logicalExpression)
            {
                ResolveTypeFromExpression(logicalExpression.Right, variables);
                return expression.TypeAnnotation = ResolveTypeFromExpression(logicalExpression.Left, variables);
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
                                if (!propertyAssignment.Expression.TypeAnnotation.Match(field.TypeAnnotation, false))
                                {
                                    throw ErrorHandler.CreateError($"Type of property assignment for property {propertyIdentifier.Name} ({propertyAssignment.Expression.TypeAnnotation.ToString()}) does not match type of field in struct {structStatement.Name} ({field.TypeAnnotation.ToString()}).", propertyAssignment);
                                }
                            }
                            else
                            {
                                throw ErrorHandler.CreateError($"Property assignment variable is not an identifier expression.", propertyAssignment);
                            }
                        }
                        return objectInitializerExpression.TypeAnnotation = new TypeAnnotation
                        {
                            IsStruct = true,
                            Struct = structStatement
                        };
                    }
                    else if(Classes.ContainsKey(identifierExpr.Name))
                    {
                        ClassStatement classStatement = Classes[identifierExpr.Name];
                        foreach (AssignmentStatement propertyAssignment in objectInitializerExpression.Fields)
                        {
                            if (propertyAssignment.Variable is IdentifierExpression propertyIdentifier)
                            {
                                VariableDeclarationStatement field = classStatement.Body.Locals.GetVariable(propertyIdentifier.Name);
                                propertyAssignment.Variable.TypeAnnotation = field.TypeAnnotation;
                                TrackTypeForExpression(propertyAssignment.Expression, variables);
                                if (!propertyAssignment.Expression.TypeAnnotation.Match(field.TypeAnnotation, false))
                                {
                                    throw ErrorHandler.CreateError($"Type of property assignment for property {propertyIdentifier.Name} ({propertyAssignment.Expression.TypeAnnotation.ToString()}) does not match type of field in class {classStatement.Name} ({field.TypeAnnotation.ToString()}).", propertyAssignment);
                                }
                            }
                            else
                            {
                                throw ErrorHandler.CreateError($"Property assignment variable is not an identifier expression.", propertyAssignment);
                            }
                        }
                        return objectInitializerExpression.TypeAnnotation = new TypeAnnotation
                        {
                            IsClass = true,
                            Class = classStatement
                        };
                    }
                    else
                    {
                        throw ErrorHandler.CreateError($"Object '{identifierExpr.Name}' not found when resolving type from object initializer expression.", objectInitializerExpression);
                    }
                }

                throw ErrorHandler.CreateError($"Expression of type {objectInitializerExpression.Expression.GetType().Name} is not supported as the expression of an object initializer expression when resolving types.");
            }
            if (expression is MemberExpression memberExpression)
            {
                ExpressionList memberChain = memberExpression.Flatten();
                StructStatement? currentStruct = null;
                ClassStatement? currentClass = null;

                bool isStruct = false;

                if (memberChain.Count <= 0)
                {
                    throw ErrorHandler.CreateError("Invalid member expression when solving type, member chain contained 0 members!", memberExpression);
                }

                memberChain.First().TypeAnnotation = ResolveTypeFromExpression(memberChain.First(), variables);

                if(!memberChain.First().TypeAnnotation.IsStruct
                    && !memberChain.First().TypeAnnotation.IsClass
                    && !memberChain.First().TypeAnnotation.IsEnum)
                {
                    throw ErrorHandler.CreateError($"First member ({memberChain.First().PrettyPrint()}) must be of struct / class type, but was of type {memberChain.First().TypeAnnotation}", memberExpression);
                }

                if(memberChain.First().TypeAnnotation.IsStruct)
                {
                    currentStruct = memberChain.First().TypeAnnotation.Struct;
                    isStruct = true;
                }
                else if (memberChain.First().TypeAnnotation.IsClass)
                {
                    currentClass = memberChain.First().TypeAnnotation.Class;
                    isStruct = false;
                }

                foreach (Expression member in memberChain.Skip(1))
                {
                    IdentifierExpression? memberIdentifier = GetInnerIdentifierExpression(member);

                    if(memberIdentifier == null)
                    {
                        throw ErrorHandler.CreateError($"Could not resolve inner identifier expression for member in member expression when resolving type from member expression.", memberExpression);
                    }

                    Console.WriteLine("------------------------------------" + member.ToString() + " ---- " + member.TypeAnnotation.ToString());

                    VariableDeclarationStatement? field = isStruct 
                        ? currentStruct.GetField(memberIdentifier.Name) 
                        : currentClass.GetField(memberIdentifier.Name);

                    Expression? type = null;

                    if(field != null)
                    {
                        type = field.Type;
                    }
                    else if(!isStruct) // check if function
                    {
                        FunctionDeclarationStatement? func = currentClass.Body.Statements.OfType<FunctionDeclarationStatement>().FirstOrDefault(f => f.Name == memberIdentifier.Name);
                        if(func == null)
                        {
                            throw ErrorHandler.CreateError($"'{memberIdentifier.Name}' does not exist in class {currentClass.Name}", memberExpression);
                        }

                        type = func.ReturnType;
                    }
                    // field.Type

                    if (type is IdentifierExpression or IndexExpression)
                    {
                        ResolveTypeFromExpression(member, [field ?? new VariableDeclarationStatement(), .. variables ?? []]);
                        member.TypeAnnotation = ResolveTypeFromExpression(type, variables);

                        if (memberChain.IsLast(member))
                        {
                            return expression.TypeAnnotation = ResolveTypeFromExpression(type, variables);
                        }

                        IdentifierExpression? fieldTypeIdentifier = GetInnerIdentifierExpression(type);
                        if (fieldTypeIdentifier == null)
                        {
                            throw ErrorHandler.CreateError($"Could not resolve inner identifier expression for field {field.Name} of struct {currentStruct.Name}: {type.TypeAnnotation.ToString()}", memberExpression);
                        }

                        // I'll need to change up this code so it properly identifies nested structs and classes
                        if (Structs.ContainsKey(fieldTypeIdentifier.Name)) 
                        {
                            currentStruct = Structs[fieldTypeIdentifier.Name];
                            isStruct = true;
                            continue;
                        }
                        else if (Classes.ContainsKey(fieldTypeIdentifier.Name))
                        {
                            currentClass = Classes[fieldTypeIdentifier.Name];
                            isStruct = false;
                            continue;
                        }
                        else
                        {
                            throw ErrorHandler.CreateError($"Struct {fieldTypeIdentifier.Name} not found when resolving type from member expression.", memberExpression);
                        }
                    }

                    if (type is TypeExpression fieldTypeExpression)
                    {
                        if (!memberChain.IsLast(member))
                        {
                            throw ErrorHandler.CreateError($"Member {memberIdentifier.Name} of struct {currentStruct.Name} is of reserved type {fieldTypeExpression.Type}, but is not the last member in the member expression chain.", memberExpression);
                        }
                        member.TypeAnnotation = ResolveTypeFromExpression(fieldTypeExpression, variables);
                        return expression.TypeAnnotation = member.TypeAnnotation;
                    }

                    throw ErrorHandler.CreateError($"Member expressions accessing field of type {type} are not supported.", memberExpression);
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
                    ReservedType = ReservedTypes.Ptr
                };
            }

            throw ErrorHandler.CreateError($"Could not resolve type of expression with type {expression.GetType().Name}", expression);
        }

        IdentifierExpression? GetInnerIdentifierExpression(Expression expression)
        {
            return expression switch
            {
                CallExpression expr => GetInnerIdentifierExpression(expr.Expression),
                IndexExpression expr => GetInnerIdentifierExpression(expr.Expression),
                ArithmeticExpression expr => GetInnerIdentifierExpression(expr.Left),
                LogicalExpression expr => GetInnerIdentifierExpression(expr.Left),
                RelationalExpression expr => GetInnerIdentifierExpression(expr.Left),
                ArrayExpression expr => GetInnerIdentifierExpression(expr.Expressions.Any() ? expr.Expressions.First() : throw ErrorHandler.CreateError($"Cannot resolve inner identifier expression of empty array", expr)),
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
                _ => throw ErrorHandler.CreateError($"Inner identifier expression of type {expression.GetType().Name} is not supported.", expression)
            };
        }

        public void TrackTypes(ClosureStatement closure)
        {
            List<StructStatement> structs = closure.Statements.OfType<StructStatement>().ToList();
            foreach (StructStatement structStatement in structs)
            {
                Structs.Add(structStatement.Name, structStatement);
            }

            List<ClassStatement> classes = closure.Statements.OfType<ClassStatement>().ToList();
            foreach (ClassStatement classStatement in classes)
            {
                Classes.Add(classStatement.Name, classStatement);
            }

            List<EnumStatement> enums = closure.Statements.OfType<EnumStatement>().ToList();
            foreach(EnumStatement enumStatement in enums)
            {
                Enums.Add(enumStatement.Name, enumStatement);
            }

            List<FunctionDeclarationStatement> functionDeclarationStatements = closure.Statements.OfType<FunctionDeclarationStatement>().ToList();
            foreach (FunctionDeclarationStatement functionDeclarationStatement in functionDeclarationStatements)
            {
                TrackTypeForParameters(functionDeclarationStatement.Parameters, closure.Locals);
                Functions.Add(functionDeclarationStatement);
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

        TypeAnnotation TrackTypeForExpression(Expression expression, Variables variables)
        {
            return expression.TypeAnnotation = ResolveTypeFromExpression(expression, variables);
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
            if(statement is ClassStatement classStatement)
            {
                TrackStatements(classStatement.Body);
                return;
            }
            if(statement is EnumStatement enumStatement)
            {
                if(enumStatement.Type == null)
                {
                    enumStatement.TypeAnnotation = new TypeAnnotation
                    {
                        IsReservedType = true,
                        ReservedType = ReservedTypes.I32,

                        IsEnum = true,
                        Enum = enumStatement
                    };
                }
                else
                {
                    TypeAnnotation enumAnnotation = TrackTypeForExpression(enumStatement.Type, variables).Copy();
                    enumAnnotation.IsEnum = true;
                    enumAnnotation.Enum = enumStatement;

                    enumStatement.TypeAnnotation = enumAnnotation;
                }
                return;
            }

            throw ErrorHandler.CreateError($"Could not resolve type of statement with type {statement.GetType().Name}", statement);
        }
    }
}
