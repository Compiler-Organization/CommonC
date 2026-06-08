using AsmResolver;
using CommonC.Error;
using CommonC.Lexer;
using CommonC.Lexer.Objects;
using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace CommonC.Parser
{
    public class SyntaxParser
    {
        LexTokenReader TokenReader { get; set; }
        string FileName { get; set; } = "unspecified";


        public SyntaxParser(LexTokenList lexTokenList, string fileName = "")
        {
            TokenReader = new LexTokenReader(lexTokenList);
            FileName = fileName;
        }

        // -- Expressions -- //

        // -- Simple Expressions -- //

        bool ParseStringExpression(out StringExpression stringExpression)
        {
            stringExpression = new StringExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if(TokenReader.Expect(LexKinds.String))
            {
                stringExpression.Value = TokenReader.Consume().Value;
                return true;
            }

            return false;
        }

        bool ParseCharacterExpression(out CharacterExpression characterExpression)
        {
            characterExpression = new CharacterExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.Char))
            {
                characterExpression.Value = TokenReader.Consume().Value[0];
                return true;
            }
            return false;
        }

        bool ParseNumberExpression(out NumberExpression numberExpression)
        {
            numberExpression = new NumberExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.Number))
            {
                if(TokenReader.Peek().Value.Contains("."))
                {
                    numberExpression.IsDouble = true;
                }

                numberExpression.Value = TokenReader.Consume().Value;
                return true;
            }

            return false;
        }

        bool ParseBooleanExpression(out BooleanExpression booleanExpression)
        {
            booleanExpression = new BooleanExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.Boolean))
            {
                string tokenValue = TokenReader.Consume().Value;
                booleanExpression.Value = tokenValue == "true";
                return true;
            }

            return false;
        }

        bool ParseIdentifierExpression(out IdentifierExpression identifierExpression)
        {
            identifierExpression = new IdentifierExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Peek().Kind == LexKinds.Identifier)
            {
                identifierExpression.Name = TokenReader.Consume().Value;
                return true;
            }
            return false;
        }

        bool ParseTypeExpression(out TypeExpression typeExpression)
        {
            typeExpression = new TypeExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            switch(TokenReader.Peek().Value)
            {
                case "string":
                case "str":
                    typeExpression.Type = ReservedTypes.String;
                    break;

                case "i8":
                    typeExpression.Type = ReservedTypes.I8;
                    break;

                case "u8":
                    typeExpression.Type = ReservedTypes.U8;
                    break;

                case "i16":
                    typeExpression.Type = ReservedTypes.I16;
                    break;

                case "u16":
                    typeExpression.Type = ReservedTypes.U16;
                    break;

                case "i32":
                    typeExpression.Type = ReservedTypes.I32;
                    break;

                case "u32":
                    typeExpression.Type = ReservedTypes.U32;
                    break;

                case "f32":
                    typeExpression.Type = ReservedTypes.F32;
                    break;

                case "f64":
                    typeExpression.Type = ReservedTypes.F64;
                    break;

                case "i64":
                    typeExpression.Type = ReservedTypes.I64;
                    break;

                case "u64":
                    typeExpression.Type = ReservedTypes.U64;
                    break;

                case "char":
                    typeExpression.Type = ReservedTypes.Char;
                    break;

                case "bool":
                    typeExpression.Type = ReservedTypes.Bool;
                    break;

                case "fn":
                    typeExpression.Type = ReservedTypes.Fn;
                    break;

                case "ptr":
                    typeExpression.Type = ReservedTypes.Ptr;
                    break;

                default:
                    return false;
            }

            TokenReader.Consume();
            return true;
        }

        bool ParseArrayExpression(out ArrayExpression arrayExpression)
        {
            arrayExpression = new ArrayExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };
            if (TokenReader.Expect(LexKinds.BraceOpen))
            {
                TokenReader.Consume();
                if (ParseExpressions(out ExpressionList expressionList))
                {
                    arrayExpression.Expressions = expressionList;
                }
                if (TokenReader.ExpectFatal(LexKinds.BraceClose))
                {
                    TokenReader.Consume();
                    return true;
                }
            }
            return false;
        }

        bool ParseLengthExpression(out LengthExpression lengthExpression)
        {
            lengthExpression = new LengthExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if(TokenReader.Expect(LexKinds.Hashtag))
            {
                TokenReader.Consume();
                if (ParseExpression(out Expression expression))
                {
                    lengthExpression.Expression = expression;
                    return true;
                }
                else
                {
                    throw ErrorHandler.CreateError("Expression expected after length symbol", lengthExpression);
                }
            }

            return false;
        }

        bool ParseParenthesizedExpression(out ParenthesizedExpression parenthesizedExpression)
        {
            parenthesizedExpression = new ParenthesizedExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if(TokenReader.Expect(LexKinds.ParentheseOpen))
            {
                TokenReader.Consume();
                if(ParseExpression(out Expression expression))
                {
                    parenthesizedExpression.Expression = expression;

                    if(TokenReader.ExpectFatal(LexKinds.ParentheseClose))
                    {
                        TokenReader.Consume();
                        return true;
                    }
                }
                else
                {
                    throw ErrorHandler.CreateError("Expression expected in parenthesized expression", parenthesizedExpression);
                }
            }

            return false;
        }

        bool ParseNullExpression(out NullExpression nullExpression)
        {
            nullExpression = new NullExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if(TokenReader.Expect(LexKinds.Keyword, "null"))
            {
                TokenReader.Consume();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses expressions without a right hand side
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        bool ParseSimpleExpression(out Expression expression)
        {
            if(ParseNullExpression(out NullExpression nullExpression))
            {
                expression = nullExpression;
                return true;
            }

            if(ParseParenthesizedExpression(out ParenthesizedExpression parenthesizedExpression))
            {
                expression = parenthesizedExpression;
                return true;
            }

            if(ParseIdentifierExpression(out IdentifierExpression identifierExpression))
            {
                expression = identifierExpression;
                return true;
            }

            if (ParseTypeExpression(out TypeExpression typeExpression))
            {
                expression = typeExpression;
                return true;
            }

            if (ParseStringExpression(out StringExpression stringExpression))
            {
                expression = stringExpression;
                return true;
            }
            if(ParseCharacterExpression(out CharacterExpression characterExpression))
            {
                expression = characterExpression;
                return true;
            }
            if (ParseNumberExpression(out NumberExpression numberExpression))
            {
                expression = numberExpression;
                return true;
            }
            if (ParseBooleanExpression(out BooleanExpression booleanExpression))
            {
                expression = booleanExpression;
                return true;
            }

            if(ParseArrayExpression(out ArrayExpression arrayExpression))
            {
                expression = arrayExpression;
                return true;
            }

            if(ParseLengthExpression(out LengthExpression lengthExpression))
            {
                expression = lengthExpression;
                return true;
            }

            if (ParseNotExpression(out NotExpression notExpression))
            {
                expression = notExpression;
                return true;
            }

            if(ParseNegateExpression(out NegateExpression negateExpression))
            {
                expression = negateExpression;
                return true;
            }

            if (ParseSizeOfExpression(out SizeOfExpression sizeOfExpression))
            {
                expression = sizeOfExpression;
                return true;
            }

            expression = null;
            return false;
        }

        /*
         
bool ParseSimpleExpression(out Expression expression)
{
    Expression result = true switch
    {
        _ when ParseNullExpression(out var expr)          => expr,
        _ when ParseParenthesizedExpression(out var expr) => expr,
        _ when ParseIdentifierExpression(out var expr)    => expr,
        _ when ParseTypeExpression(out var expr)          => expr,
        _ when ParseStringExpression(out var expr)        => expr,
        _ when ParseCharacterExpression(out var expr)     => expr,
        _ when ParseNumberExpression(out var expr)        => expr,
        _ when ParseBooleanExpression(out var expr)       => expr,
        _ when ParseArrayExpression(out var expr)         => expr,
        _ when ParseLengthExpression(out var expr)        => expr,
        _ when ParseNotExpression(out var expr)           => expr,
        _ when ParseNegateExpression(out var expr)        => expr,
        _ when ParseSizeOfExpression(out var expr)        => expr,
        _                                                 => null
    };

    if (result != null)
    {
        result.YourProperty = yourValue; //
        expression = result;
        return true;
    }

    expression = null;
    return false;
}


         */


        // -- Complex Expressions -- //

        bool ParseCallExpression(Expression expression, out CallExpression callExpression)
        {
            callExpression = new CallExpression()
            {
                Expression = expression,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.ParentheseOpen))
            {
                TokenReader.Consume();
                if (ParseExpressions(out ExpressionList expressionList))
                {
                    callExpression.Arguments = expressionList;
                }

                if (TokenReader.ExpectFatal(LexKinds.ParentheseClose))
                {
                    TokenReader.Consume();
                    return true;
                }
            }
            return false;
        }

        bool ParseArithmeticExpression(Expression leftExpression, out ArithmeticExpression arithmeticExpression)
        {
            arithmeticExpression = new ArithmeticExpression()
            {
                Left = leftExpression,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            var kind = TokenReader.Peek().Kind;

            if (!Enum.IsDefined(typeof(ArithmeticOperator), (int)kind))
            {
                return false;
            }

            arithmeticExpression.Operator = (ArithmeticOperator)kind;

            TokenReader.Consume();

            if (ParseExpression(out Expression rightExpression))
            {
                arithmeticExpression.Right = rightExpression;
                return true;
            }

            throw ErrorHandler.CreateError("Invalid right hand expression when parsing arithemtic expression", leftExpression);
        }

        bool ParseLogicalExpression(Expression leftExpression, out LogicalExpression logicalExpression)
        {
            logicalExpression = new LogicalExpression()
            {
                Left = leftExpression,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            LexKinds arithmeticKind = TokenReader.Peek().Kind;
            switch (arithmeticKind)
            {
                case LexKinds.And:
                case LexKinds.Or:
                    logicalExpression.Operator = (LogicalOperator)arithmeticKind;
                    break;

                default:
                    return false;
            }

            TokenReader.Consume();

            if (ParseExpression(out Expression rightExpression))
            {
                logicalExpression.Right = rightExpression;
                return true;
            }

            throw ErrorHandler.CreateError("Invalid right hand expression when parsing logical expression", leftExpression);
        }

        bool ParseRangeExpression(Expression leftExpression, out RangeExpression rangeExpression)
        {
            rangeExpression = new RangeExpression()
            {
                Start = leftExpression,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.Range))
            {
                TokenReader.Skip(1);
                if (ParseExpression(out Expression rightExpression))
                {
                    rangeExpression.End = rightExpression;
                    return true;
                }
                throw ErrorHandler.CreateError("Invalid right hand expression when parsing range expression", leftExpression);
            }
            return false;
        }

        bool ParseMemberExpression(Expression parentExpression, out MemberExpression memberExpression)
        {
            memberExpression = new MemberExpression()
            {
                Parent = parentExpression,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.Dot))
            {
                TokenReader.Consume();
                
                if (ParseSimpleExpression(out Expression childExpression))
                {
                    while (ParseIndexExpression(childExpression, out IndexExpression indexExpression))
                    {
                        childExpression = indexExpression;
                    }

                    memberExpression.Member = childExpression;
                    return true;
                }
                throw ErrorHandler.CreateError("Invalid member expression, expected a simple expression after the punctation", parentExpression);
            }
            return false;
        }

        bool ParseIndexExpression(Expression expression, out IndexExpression indexExpression)
        {
            indexExpression = new IndexExpression()
            {
                Expression = expression,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.BracketOpen))
            {
                TokenReader.Consume();
                if (ParseExpression(out Expression indexerExpression))
                {
                    indexExpression.Index = indexerExpression;
                }
                if (TokenReader.ExpectFatal(LexKinds.BracketClose))
                {
                    TokenReader.Consume();
                    return true;
                }
            }
            return false;
        }

        bool ParseUnpackExpression(Expression leftExpression, out UnpackExpression unpackExpression)
        {
            unpackExpression = new UnpackExpression()
            {
                Left = leftExpression,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if(TokenReader.Expect(LexKinds.Subtraction) && TokenReader.Expect(LexKinds.ChevronClose, 1))
            {
                TokenReader.Skip(2);
                if (ParseExpression(out Expression rightExpression))
                {
                    unpackExpression.Right = rightExpression;
                    return true;
                }
                throw ErrorHandler.CreateError("Invalid right hand expression when parsing unpack expression", leftExpression);
            }
            return false;
        }

        bool ParseRelationalExpression(Expression leftExpression, out RelationalExpression relationalExpression)
        {
            relationalExpression = new RelationalExpression
            {
                Left = leftExpression,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            switch (TokenReader.Peek().Kind)
            {
                case LexKinds.EqualTo:
                case LexKinds.NotEqualTo:
                case LexKinds.BiggerOrEqual:
                case LexKinds.SmallerOrEqual:
                case LexKinds.ChevronOpen:
                case LexKinds.ChevronClose:
                    {
                        switch (TokenReader.Peek().Kind)
                        {
                            case LexKinds.Equals: relationalExpression.Operator = RelationalOperators.Equal; break;
                            case LexKinds.NotEqualTo: relationalExpression.Operator = RelationalOperators.NotEqual; break;
                            case LexKinds.BiggerOrEqual: relationalExpression.Operator = RelationalOperators.GreaterThanOrEqual; break;
                            case LexKinds.SmallerOrEqual: relationalExpression.Operator = RelationalOperators.LessThanOrEqual; break;
                            case LexKinds.ChevronOpen: relationalExpression.Operator = RelationalOperators.LessThan; break;
                            case LexKinds.ChevronClose: relationalExpression.Operator = RelationalOperators.GreaterThan; break;
                        }

                        TokenReader.Skip(1);
                        if (ParseExpression(out Expression right))
                        {
                            relationalExpression.Right = right;
                            return true;
                        }
                        throw ErrorHandler.CreateError("Invalid right hand expression when parsing relational expression", leftExpression);
                    }
            }

            return false;
        }

        bool ParseParameterExpression(out ParameterExpression parameterExpression)
        {
            parameterExpression = new ParameterExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if(ParseTypeExpression(out TypeExpression typeExpression))
            {
                Expression currentTypeLayout = typeExpression;

                while (ParseIndexExpression(currentTypeLayout, out IndexExpression indexExpression))
                {
                    currentTypeLayout = indexExpression;
                }

                parameterExpression.Type = currentTypeLayout;
            }
            else
            {
                if (ParseIdentifierExpression(out IdentifierExpression identifierExpression))
                {
                    if (ParseMemberExpression(identifierExpression, out MemberExpression memberExpression))
                    {
                        parameterExpression.Type = memberExpression;
                    }
                    else if(ParseIndexExpression(identifierExpression, out IndexExpression indexExpression))
                    {
                        parameterExpression.Type = indexExpression;
                    }
                    else
                    {
                        parameterExpression.Type = identifierExpression;
                    }
                }
                else
                {
                    return false;
                }
            }

            

            if (ParseIdentifierExpression(out IdentifierExpression nameExpression))
            {
                parameterExpression.Name = nameExpression.Name;
            }
            else
            {
                throw ErrorHandler.CreateError("Invalid parameter expression, expected an identifier after the type expression", parameterExpression);
            }

            if(TokenReader.Expect(LexKinds.Equals))
            {
                TokenReader.Consume();
                if(ParseExpression(out Expression defaultExpression))
                {
                    parameterExpression.Value = defaultExpression;
                }
            }

            return true;
        }

        bool ParseParameterExpressions(out ParameterExpressionList parameters)
        {
            parameters = new ParameterExpressionList();

            for (; ; )
            {
                if (ParseParameterExpression(out ParameterExpression parameter))
                {
                    parameters.Add(parameter);
                    continue;
                }

                if(TokenReader.Expect(LexKinds.Vararg))
                {
                    TokenReader.Consume();
                    parameters.IsVararg = true;
                    continue;
                }

                if (TokenReader.Expect(LexKinds.Comma))
                {
                    TokenReader.Consume();
                    continue;
                }

                break;
            }

            return parameters.Count > 0;
        }

        bool ParseArrayInitializerExpression(IndexExpression indexExpression, out ArrayInitializerExpression arrayInitializerExpression)
        {
            arrayInitializerExpression = new ArrayInitializerExpression() 
            {
                Index = indexExpression,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if(ParseArrayExpression(out ArrayExpression arrayExpression))
            {
                arrayInitializerExpression.Array = arrayExpression;
                return true;
            }

            return false;
        }

        bool ParseNotExpression(out NotExpression notExpression)
        {
            notExpression = new NotExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if(TokenReader.Expect(LexKinds.Exclamation))
            {
                TokenReader.Consume();
                if(ParseExpression(out Expression expression))
                {
                    notExpression.Expression = expression;
                    return true;
                }
                else
                {
                    throw ErrorHandler.CreateError("Expression expected when parsing not expression", notExpression);
                }
            }

            return false;
        }

        bool ParseNegateExpression(out NegateExpression negateExpression)
        {
            negateExpression = new NegateExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if(TokenReader.Expect(LexKinds.Subtraction))
            {
                TokenReader.Consume();
                if (ParseExpression(out Expression expression))
                {
                    negateExpression.Expression = expression;
                    return true;
                }
                else
                {
                    throw ErrorHandler.CreateError("Expression expected when parsing negate expression", negateExpression);
                }
            }

            return false;
        }

        bool ParseObjectInitializerExpression(Expression expression, out ObjectInitializerExpression objectInitializerExpression)
        {
            objectInitializerExpression = new ObjectInitializerExpression()
            {
                Expression = expression
            };

            if(expression is IdentifierExpression
                || expression is MemberExpression)
            {
                if(TokenReader.Expect(LexKinds.BraceOpen))
                {
                    TokenReader.Consume();
                    for(; ; )
                    {
                        if(ParseExpression(out Expression assignmentVariableExpression, true))
                        {
                            if (TokenReader.ExpectFatal(LexKinds.Colon))
                            {
                                TokenReader.Consume();
                                if (ParseExpression(out Expression valueExpression))
                                {
                                    objectInitializerExpression.Fields.Add(new AssignmentStatement
                                    {
                                        Variable = assignmentVariableExpression,
                                        Expression = valueExpression
                                    });
                                    continue;
                                }
                                else
                                {
                                    throw ErrorHandler.CreateError("Failed to read property assignment expression in object initializer", assignmentVariableExpression);
                                }
                            }
                        }

                        if(TokenReader.Expect(LexKinds.Comma))
                        {
                            TokenReader.Consume();
                            continue;
                        }

                        break;
                    }

                    if(TokenReader.ExpectFatal(LexKinds.BraceClose))
                    {
                        TokenReader.Consume();
                        return true;
                    }
                }
            }

            return false;
        }

        bool ParseSizeOfExpression(out SizeOfExpression sizeOfExpression)
        {
            sizeOfExpression = new SizeOfExpression()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.Keyword, "sizeof"))
            {
                TokenReader.Skip(1);
                if (ParseExpression(out Expression expression))
                {
                    sizeOfExpression.Expression = expression;
                    return true;
                }
                else
                {
                    
                    throw ErrorHandler.CreateError("Expression expected after sizeof keyword", sizeOfExpression);
                }
            }

            return false;
        }

        bool ParseExpression(out Expression expression, bool parseSimple = false)
        {
            if (ParseSimpleExpression(out Expression simpleExpression))
            {
                expression = simpleExpression;
            }
            else
            {
                expression = null;
                return false;
            }

            // Parse expressions with a right hand side.
            for(; ; )
            {
                if(!parseSimple)
                {
                    if (ParseUnpackExpression(expression, out UnpackExpression unpackExpression))
                    {
                        expression = unpackExpression;
                        continue;
                    }

                    if (ParseCallExpression(expression, out CallExpression callExpression))
                    {
                        expression = callExpression;
                        continue;
                    }
                    if (ParseArithmeticExpression(expression, out ArithmeticExpression arithmeticExpression))
                    {
                        expression = arithmeticExpression;
                        continue;
                    }
                    if(ParseLogicalExpression(expression, out LogicalExpression logicalExpression))
                    {
                        expression = logicalExpression;
                        continue;
                    }
                    if (ParseRangeExpression(expression, out RangeExpression rangeExpression))
                    {
                        expression = rangeExpression;
                        continue;
                    }
                    if (ParseRelationalExpression(expression, out RelationalExpression relationalExpression))
                    {
                        expression = relationalExpression;
                        continue;
                    }
                }

                if (ParseIndexExpression(expression, out IndexExpression indexExpression))
                {
                    if(ParseArrayInitializerExpression(indexExpression, out ArrayInitializerExpression arrayInitializerExpression))
                    {
                        expression = arrayInitializerExpression;
                    }
                    else
                    {
                        expression = indexExpression;
                    }
                    continue;
                }
                if (ParseMemberExpression(expression, out MemberExpression memberExpression))
                {
                    expression = memberExpression;
                    continue;
                }

                if(ParseObjectInitializerExpression(expression, out ObjectInitializerExpression objectInitializerExpression))
                {
                    expression = objectInitializerExpression;
                    continue;
                }

                return true;
            }
        }

        bool ParseExpressions(out ExpressionList expressionList)
        {
            expressionList = new ExpressionList();

            for (; ; )
            {
                if (ParseExpression(out Expression statement))
                {
                    expressionList.Add(statement);
                    continue;
                }

                if(TokenReader.Expect(LexKinds.Comma))
                {
                    TokenReader.Consume();
                    continue;
                }

                break;
            }

            return expressionList.Count > 0;
        }


        // -- Statements -- //

        bool ParseClosureStatement(out ClosureStatement closureStatement)
        {
            closureStatement = new ClosureStatement()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if(TokenReader.Expect(LexKinds.BraceOpen))
            {
                TokenReader.Consume();
                if(ParseStatements(out StatementList statementList))
                {
                    closureStatement.Statements = statementList;
                }

                if(TokenReader.ExpectFatal(LexKinds.BraceClose))
                {
                    TokenReader.Consume();
                    return true;
                }
            }

            return false;
        }

        bool ParseCallStatement(Expression expression, out CallStatement callStatement)
        {
            callStatement = new CallStatement()
            {
                Expression = expression,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.ParentheseOpen))
            {
                TokenReader.Consume();
                if (ParseExpressions(out ExpressionList expressionList))
                {
                    callStatement.Arguments = expressionList;
                }

                if (TokenReader.ExpectFatal(LexKinds.ParentheseClose))
                {
                    TokenReader.Consume();
                    return true;
                }
            }
            return false;
        }

        bool ParseFunctionDeclarationStatement(Expression typeExpression, IdentifierExpression nameExpression, bool isExtern, out FunctionDeclarationStatement functionDeclarationStatement)
        {
            functionDeclarationStatement = new FunctionDeclarationStatement() 
            {
                ReturnType = typeExpression,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            functionDeclarationStatement.Name = nameExpression.Name;

            if (TokenReader.Expect(LexKinds.ParentheseOpen))
            {
                TokenReader.Consume();
                if (ParseParameterExpressions(out ParameterExpressionList parameters))
                {
                    functionDeclarationStatement.Parameters = parameters;
                }

                if (TokenReader.ExpectFatal(LexKinds.ParentheseClose))
                {
                    TokenReader.Consume();
                }
            }

            if (ParseClosureStatement(out ClosureStatement closureStatement))
            {
                functionDeclarationStatement.Body = closureStatement;
                return true;
            }

            if (isExtern)
            {
                functionDeclarationStatement.IsExtern = true;
                return true;
            }

            return false;
        }

        bool ParseVariableDeclarationStatement(Expression typeExpression, IdentifierExpression nameExpression, out VariableDeclarationStatement variableDeclarationStatement)
        {
            variableDeclarationStatement = new VariableDeclarationStatement()
            {
                Type = typeExpression,
                Name = nameExpression.Name,
                Line = TokenReader.Peek().Line, FileName = FileName
            };
            if (TokenReader.Expect(LexKinds.Equals))
            {
                TokenReader.Consume();
                if (ParseExpression(out Expression valueExpression))
                {
                    variableDeclarationStatement.Expression = valueExpression;
                    return true;
                }
                throw ErrorHandler.CreateError($"Invalid variable declaration statement", variableDeclarationStatement);
            }

            return true;
        }

        bool ParseIfStatement(out IfStatement ifStatement)
        {
            ifStatement = new IfStatement()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.Keyword, "if"))
            {
                TokenReader.Skip(1);

                if (ParseExpression(out Expression conditionExpression))
                {
                    ifStatement.Condition = conditionExpression;
                }
                else
                {
                    throw ErrorHandler.CreateError($"Invalid if statement, expected a condition expression", ifStatement);
                }

                // Parse if closure
                if (TokenReader.Expect(LexKinds.BraceOpen))
                {
                    if (ParseClosureStatement(out ClosureStatement closureStatement))
                    {
                        ifStatement.Body = closureStatement;
                    }
                }
                else if(ParseStatement(out Statement statement))
                {
                    ifStatement.Body.Statements.Add(statement);
                }

                // Parse elseifs
                for (; ; )
                {
                    if(TokenReader.Expect(LexKinds.Keyword, "elseif"))
                    {
                        IfStatement elseIfStatement = new IfStatement()
                        {
                            Line = TokenReader.Peek().Line, FileName = FileName
                        };

                        TokenReader.Skip(1);
                        if (ParseExpression(out Expression elseIfConditionExpression))
                        {
                            elseIfStatement.Condition = elseIfConditionExpression;
                        }
                        else
                        {
                            throw ErrorHandler.CreateError($"Invalid elseif statement, expected a condition expression", elseIfStatement);
                        }

                        if (TokenReader.Expect(LexKinds.BraceOpen))
                        {
                            if (ParseClosureStatement(out ClosureStatement elseIfClosureStatement))
                            {
                                elseIfStatement.Body = elseIfClosureStatement;
                                ifStatement.ElseIfs.Add(elseIfStatement);
                            }
                            else
                            {
                                throw ErrorHandler.CreateError($"Invalid elseif statement, expected a complete closure", elseIfStatement);
                            }

                            continue;
                        }
                        else if (ParseStatement(out Statement elseIfStatementBody))
                        {
                            elseIfStatement.Body.Statements.Add(elseIfStatementBody);
                            ifStatement.ElseIfs.Add(elseIfStatement);
                            continue;
                        }


                        throw ErrorHandler.CreateError($"Invalid elseif statement, could not parse elseif", elseIfStatement);
                    }

                    break;
                }

                // Parse else
                if (TokenReader.Expect(LexKinds.Keyword, "else"))
                {
                    TokenReader.Skip(1);
                    if (TokenReader.Expect(LexKinds.BraceOpen))
                    {
                        if (ParseClosureStatement(out ClosureStatement elseClosureStatement))
                        {
                            ifStatement.Else = elseClosureStatement;
                        }
                    }
                    else if (ParseStatement(out Statement elseStatement))
                    {
                        ifStatement.Else.Statements.Add(elseStatement);
                    }
                }

                return true;
            }

            return false;
        }

        bool ParseAssignmentStatement(Expression variable, out AssignmentStatement assignmentStatement)
        {
            assignmentStatement = new AssignmentStatement 
            { 
                Variable = variable,
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            var peekedToken = TokenReader.Peek();
            var kind = peekedToken.Kind;

            if (!Enum.IsDefined(typeof(AssignmentOperator), (int)kind))
            {
                return false;
            }

            assignmentStatement.Operator = (AssignmentOperator)kind;
            TokenReader.Consume();

            if (ParseExpression(out Expression valueExpression))
            {
                assignmentStatement.Expression = valueExpression;
                return true;
            }

            throw ErrorHandler.CreateError($"Invalid assignment statement, expected an expression after the equals sign", assignmentStatement);
        }


        bool ParseForStatement(out ForStatement forStatement)
        {
            forStatement = new ForStatement()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.Keyword, "for"))
            {
                TokenReader.Skip(1);

                if (ParseExpression(out Expression expression))
                {
                    if (expression is RangeExpression rangeExpression)
                    {
                        forStatement.Range = rangeExpression;
                    }
                    else
                    {
                        throw ErrorHandler.CreateError($"Invalid for statement, expected a range expression after the 'for' keyword, got {expression.GetType().FullName} ({TokenReader.Peek().Kind}, {TokenReader.Peek().Value})", forStatement);
                    }
                }
                else
                {
                    throw ErrorHandler.CreateError($"Line {TokenReader.Peek().Line}: Invalid for statement, expected expression after the 'for' keyword", forStatement);
                }

                if (TokenReader.ExpectFatal(LexKinds.Comma))
                {
                    TokenReader.Consume();
                }

                if (ParseIdentifierExpression(out IdentifierExpression identifierExpression))
                {
                    forStatement.Variable = new VariableDeclarationStatement
                    {
                        Name = identifierExpression.Name,
                        Expression = new NumberExpression { Value = "0" },
                        Type = new TypeExpression { Type = ReservedTypes.I32 },
                        Line = TokenReader.Peek().Line, FileName = FileName
                    };
                }
                else
                {
                    throw ErrorHandler.CreateError($"Line {TokenReader.Peek().Line}: Invalid for statement, expected an identifier after the range expression and comma", identifierExpression);
                }

                // Parse body
                if (TokenReader.Expect(LexKinds.BraceOpen))
                {
                    if (ParseClosureStatement(out ClosureStatement closureStatement))
                    {
                        forStatement.Body = closureStatement;
                        return true;
                    }
                }
                else if (ParseStatement(out Statement bodyStatement))
                {
                    forStatement.Body.Statements.Add(bodyStatement);
                    return true;
                }
            }
            return false;
        }

        bool ParseReturnStatement(out ReturnStatement returnStatement)
        {
            returnStatement = new ReturnStatement()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };
            if (TokenReader.Expect(LexKinds.Keyword, "return")
                || TokenReader.Expect(LexKinds.Keyword, "ret"))
            {
                TokenReader.Skip(1);
                if (ParseExpression(out Expression returnExpression))
                {
                    returnStatement.Expression = returnExpression;
                }
                return true;
            }
            return false;
        }

        bool ParseWhileStatement(out WhileStatement whileStatement)
        {
            whileStatement = new WhileStatement()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            }; ;

            if(TokenReader.Expect(LexKinds.Keyword, "while"))
            {
                TokenReader.Skip(1);
                if(ParseExpression(out Expression expression))
                {
                    whileStatement.Expression = expression;
                }
                else
                {
                    throw ErrorHandler.CreateError("Expression expected after while keyword", whileStatement);
                }

                if(ParseClosureStatement(out ClosureStatement closureStatement))
                {
                    whileStatement.Body = closureStatement;
                    return true;
                }
                else if (ParseStatement(out Statement bodyStatement))
                {
                    whileStatement.Body.Statements.Add(bodyStatement);
                    return true;
                }
            }

            return false;
        }

        bool ParseStructStatement(out StructStatement structStatement)
        {
            structStatement = new StructStatement()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if(TokenReader.Expect(LexKinds.Keyword, "struct"))
            {
                TokenReader.Consume();
                if(TokenReader.ExpectFatal(LexKinds.Identifier))
                {
                    structStatement.Name = TokenReader.Consume().Value;
                }

                if (TokenReader.ExpectFatal(LexKinds.BraceOpen))
                {
                    TokenReader.Consume();
                }

                int fieldIndex = 0;
                for(; ; )
                {
                    if(ParseExpression(out Expression expression, true))
                    {
                        if (expression is TypeExpression
                            || expression is IdentifierExpression
                            || expression is MemberExpression
                            || expression is IndexExpression)
                        {

                            if (ParseIdentifierExpression(out IdentifierExpression nameExpression))
                            {
                                if (ParseVariableDeclarationStatement(expression, nameExpression, out VariableDeclarationStatement variableDeclarationStatement))
                                {
                                    variableDeclarationStatement.FieldIndex = fieldIndex++;
                                    structStatement.Fields.Add(variableDeclarationStatement);
                                }
                            }
                        }
                        else
                        {
                            throw ErrorHandler.CreateError($"Line {TokenReader.Peek().Line}: Type / identifier / member expected when parsing field in struct declaration.", structStatement);
                        }
                    }

                    if(TokenReader.Expect(LexKinds.Comma))
                    {
                        TokenReader.Consume();
                        continue;
                    }

                    break;
                }

                if(TokenReader.ExpectFatal(LexKinds.BraceClose))
                {
                    TokenReader.Consume();
                }

                return true;
            }

            return false;
        }

        bool ParseClassStatement(out ClassStatement classStatement)
        {
            classStatement = new ClassStatement()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.Keyword, "class"))
            {
                TokenReader.Consume();
                if (TokenReader.ExpectFatal(LexKinds.Identifier))
                {
                    classStatement.Name = TokenReader.Consume().Value;
                }

                if (ParseClosureStatement(out ClosureStatement closureStatement))
                {
                    classStatement.Body = closureStatement;
                    return true;
                }

                throw ErrorHandler.CreateError("Could not parse body of class statement", classStatement);
            }

            return false;
        }

        bool ParseUseStatement(out UseStatement useStatement)
        {
            useStatement = new UseStatement()
            {
                Line = TokenReader.Peek().Line, FileName = FileName
            };

            if (TokenReader.Expect(LexKinds.Keyword, "use"))
            {
                TokenReader.Skip(1);
                if (ParseIdentifierExpression(out IdentifierExpression identifierExpression))
                {
                    useStatement.Identifier = identifierExpression;
                }
                return true;
            }
            return false;
        }

        bool ParseEnumStatement(out EnumStatement enumStatement)
        {
            enumStatement = new EnumStatement()
            {
                Line = TokenReader.Peek().Line,
                FileName = FileName
            };

            if(TokenReader.Expect(LexKinds.Keyword, "enum"))
            {
                TokenReader.Consume();
                if (ParseIdentifierExpression(out IdentifierExpression identifierExpression))
                {
                    enumStatement.Name = identifierExpression.Name;
                }
                else
                {
                    throw ErrorHandler.CreateError($"Could not parse name of enum, invalid syntax", enumStatement);
                }

                if (TokenReader.Expect(LexKinds.Colon))
                {
                    TokenReader.Consume();
                    if(ParseTypeExpression(out TypeExpression typeExpression))
                    {
                        enumStatement.Type = typeExpression;
                    }
                    else
                    {
                        throw ErrorHandler.CreateError($"Could not parse type of enum, invalid syntax", enumStatement);
                    }
                }

                TokenReader.ExpectFatal(LexKinds.BraceOpen);
                TokenReader.Consume();

                ulong index = 0;
                for(; ; )
                {
                    if(ParseIdentifierExpression(out IdentifierExpression variantIdentifier))
                    {
                        EnumVariant variant = new EnumVariant
                        {
                            Name = variantIdentifier.Name
                        };

                        enumStatement.Variants.Add(variant);

                        if (TokenReader.Expect(LexKinds.Colon))
                        {
                            TokenReader.Consume();
                            if (ParseNumberExpression(out NumberExpression variantNumber))
                            {
                                variant.Expression = variantNumber;
                                index = variantNumber.ToUlong();
                                index++;
                            }
                        }
                        else
                        {
                            variant.Expression = new NumberExpression
                            {
                                Value = index.ToString()
                            };

                            index++;
                        }

                        if (TokenReader.Expect(LexKinds.Comma))
                        {
                            TokenReader.Consume();
                            continue;
                        }

                        break;
                    }
                }


                TokenReader.ExpectFatal(LexKinds.BraceClose);
                TokenReader.Consume();

                return true;
            }

            return false;
        }

        enum Test : int
        {
            variant,
            variant2 = 1000,
            variant3,
        }

        bool ParseStatement(out Statement statement)
        {
            if(ParseEnumStatement(out EnumStatement enumStatement))
            {
                statement = enumStatement;
                return true;
            }

            if(ParseClassStatement(out ClassStatement classStatement))
            {
                statement = classStatement;
                return true;
            }

            if(ParseStructStatement(out StructStatement structStatement))
            {
                statement = structStatement;
                return true;
            }

            if(ParseIfStatement(out IfStatement ifStatement))
            {
                statement = ifStatement;
                return true;
            }

            if(ParseForStatement(out ForStatement forStatement))
            {
                statement = forStatement;
                return true;
            }

            if(ParseReturnStatement(out ReturnStatement returnStatement))
            {
                statement = returnStatement;
                return true;
            }

            if(ParseWhileStatement(out WhileStatement whileStatement))
            {
                statement = whileStatement;
                return true;
            }

            if(ParseUseStatement(out UseStatement useStatement))
            {
                statement = useStatement;
                return true;
            }

            bool isExtern = false;

            if (TokenReader.Expect(LexKinds.Keyword, "extern"))
            {
                TokenReader.Consume();
                isExtern = true;
            }

            if (ParseExpression(out Expression expression, true))
            {
                if(ParseCallStatement(expression, out CallStatement callStatement))
                {
                    statement = callStatement;
                    return true;
                }

                if(expression is TypeExpression 
                    || expression is IdentifierExpression
                    || expression is MemberExpression
                    || expression is IndexExpression)
                {

                    if(ParseIdentifierExpression(out IdentifierExpression nameExpression))
                    {
                        if (ParseFunctionDeclarationStatement(expression, nameExpression, isExtern, out FunctionDeclarationStatement functionDeclarationStatement))
                        {
                            statement = functionDeclarationStatement;
                            return true;
                        }

                        if (ParseVariableDeclarationStatement(expression, nameExpression, out VariableDeclarationStatement variableDeclarationStatement))
                        {
                            statement = variableDeclarationStatement;
                            return true;
                        }

                    }

                }

                if (ParseAssignmentStatement(expression, out AssignmentStatement assignmentStatement))
                {
                    statement = assignmentStatement;
                    return true;
                }
            }

            statement = null;
            return false;
        }

        bool ParseStatements(out StatementList statementList)
        {
            statementList = new StatementList();

            for(; ; )
            {
                if (ParseStatement(out Statement statement))
                {
                    statementList.Add(statement);
                    continue;
                }

                if (TokenReader.Peek().Kind == LexKinds.Semicolon)
                {
                    TokenReader.Consume();
                    continue;
                }

                break;
            }

            return statementList.Count > 0;
        }

        /// <summary>
        /// Parses the lexical token list into an abstract syntax tree. See CommonC.Parser.<see cref="CommonC.Parser.AST"/> for more information on the abstract syntax tree.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public ClosureStatement ParseLexTokenList()
        {
            TokenReader.LexTokens.RemoveAll(token => token.Kind == LexKinds.NewLine);

            if (ParseStatements(out StatementList statementList))
            {
                return new ClosureStatement
                {
                    Statements = statementList
                };
            }

            throw ErrorHandler.CreateError("Failed to parse the lex token list, is code valid?");
        }
    }
}
