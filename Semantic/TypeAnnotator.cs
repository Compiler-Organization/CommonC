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
        Dictionary<string, StructStatement> Structs = new Dictionary<string, StructStatement>();
        Dictionary<string, FunctionDeclarationStatement> Functions = new Dictionary<string, FunctionDeclarationStatement>();

        TypeAnnotation ResolveTypeFromExpression(Expression expression, Variables? variables)
        {
            if (expression is StringExpression)
            {
                return new TypeAnnotation
                {
                    IsReservedType = true,
                    ReservedType = ReservedTypes.String
                };
            }
            if (expression is NumberExpression numberExpression)
            {
                if (numberExpression.IsDouble)
                {
                    return new TypeAnnotation
                    {
                        IsReservedType = true,
                        ReservedType = ReservedTypes.F64
                    };
                }
                else
                {
                    return new TypeAnnotation
                    {
                        IsReservedType = true,
                        ReservedType = ReservedTypes.I32
                    };
                }
            }
            if (expression is TypeExpression typeExpression)
            {
                return new TypeAnnotation
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

                return ResolveTypeFromExpression(variables.GetVariable(identifierExpression.Name).Type, variables);
            }
            if (expression is CallExpression callExpression)
            {
                if (callExpression.Expression is IdentifierExpression callIdentifierExpression)
                {
                    if (Functions.ContainsKey(callIdentifierExpression.Name))
                    {
                        return ResolveTypeFromExpression(Functions[callIdentifierExpression.Name].ReturnType, variables);
                    }
                }

                throw new Exception($"Call expression of type {callExpression.Expression.GetType().Name} is not supported when resolving LLVM types from expressions.");
            }
            if (expression is IndexExpression indexExpression)
            {
                TypeAnnotation indexTypeAnnotation = ResolveTypeFromExpression(indexExpression.Expression, variables);
                indexTypeAnnotation.IsArray = true;
                return indexTypeAnnotation;
            }
            if (expression is NotExpression notExpression)
            {
                return ResolveTypeFromExpression(notExpression.Expression, variables);
            }
            if (expression is BooleanExpression booleanExpression)
            {
                return new TypeAnnotation
                {
                    IsReservedType = true,
                    ReservedType = ReservedTypes.Bool
                };
            }
            if (expression is ParenthesizedExpression parenthesizedExpression)
            {
                return ResolveTypeFromExpression(parenthesizedExpression.Expression, variables);
            }
            if (expression is RelationalExpression relationalExpression)
            {
                return ResolveTypeFromExpression(relationalExpression.Left, variables);
            }
            if (expression is ArithmeticExpression arithmeticExpression)
            {
                return ResolveTypeFromExpression(arithmeticExpression.Left, variables);
            }
            if (expression is ArrayInitializerExpression arrayInitializerExpression)
            {
                return ResolveTypeFromExpression(arrayInitializerExpression.Index, variables);
            }
            if (expression is SizeOfExpression sizeOfExpression)
            {
                return ResolveTypeFromExpression(sizeOfExpression.Expression, variables);
            }
            if (expression is LengthExpression lengthExpression)
            {
                return ResolveTypeFromExpression(lengthExpression.Expression, variables);
            }
            if (expression is MemberExpression memberExpression)
            {
                ExpressionList memberChain = memberExpression.Flatten();
                StructStatement? currentStruct = null;

                if (memberChain.Count <= 0)
                {
                    throw new Exception("Invalid member expression when solving LLVM type, cannot be 0!");
                }

                if (memberChain.First() is IdentifierExpression firstIdentifier)
                {
                    if (variables == null)
                    {
                        throw new Exception($"Variables cannot be null when resolving LLVM type from member expression with identifier {firstIdentifier.Name} as first member.");
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
                    throw new Exception($"Could not resolve struct for member expression when resolving LLVM type.");
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
                        return ResolveTypeFromExpression(field.Type, variables); // may cause issues as variables include all variables in the current scope, including parameters and local variables.
                    }
                }
            }

            throw new Exception($"Expression {expression.GetType().Name} could not be resolved to an LLVM type.");
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

        void AnnotateTypes(ClosureStatement closure)
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

            AnnotateStatements(closure.Statements, closure.Locals);
        }

        void AnnotateTypeForExpression(Expression expression, Variables variables)
        {
            if (expression == null) return;
            if (expression is IdentifierExpression identifierExpression)
            {
                if (Structs.TryGetValue(identifierExpression.Name, out StructStatement? structStatement))
                {
                    expression.TypeAnnotation = new TypeAnnotation
                    {
                        IsStruct = true,
                        Struct = structStatement
                    };
                }

                if (variables.Contains(identifierExpression.Name))
                {
                    VariableDeclarationStatement variable = variables.GetVariable(identifierExpression.Name);
                    expression.TypeAnnotation = ResolveTypeFromExpression(variable.Type, variables);
                }
                return;
            }
            if (expression is CallExpression callExpression)
            {

                return;
            }
        }

        void AnnotateStatements(StatementList statements, Variables variables)
        {
            foreach (Statement statement in statements)
            {
                if (statement is FunctionDeclarationStatement functionDeclarationStatement)
                {

                    continue;
                }
                if (statement is IfStatement ifStatement)
                {

                    continue;
                }
                if (statement is ForStatement forStatement)
                {

                    continue;
                }
            }
        }
    }
}
