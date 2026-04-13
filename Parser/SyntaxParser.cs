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

        public SyntaxParser(LexTokenList lexTokenList)
        {
            TokenReader = new LexTokenReader(lexTokenList);
        }

        // -- Expressions -- //

        // -- Simple Expressions -- //

        bool ParseStringExpression(out StringExpression stringExpression)
        {
            stringExpression = new StringExpression();

            if(TokenReader.Expect(LexKinds.String))
            {
                stringExpression.Value = TokenReader.Consume().Value;
                return true;
            }

            return false;
        }

        bool ParseNumberExpression(out NumberExpression numberExpression)
        {
            numberExpression = new NumberExpression();

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
            booleanExpression = new BooleanExpression();

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
            identifierExpression = new IdentifierExpression();
            if (TokenReader.Peek().Kind == LexKinds.Identifier)
            {
                identifierExpression.Name = TokenReader.Consume().Value;
                return true;
            }
            return false;
        }

        bool ParseTypeExpression(out TypeExpression typeExpression)
        {
            typeExpression = new TypeExpression();

            switch(TokenReader.Peek().Value)
            {
                case "string":
                case "str":
                    typeExpression.Type = ReservedTypes.String;
                    break;

                case "integer":
                case "int":
                case "i32":
                    typeExpression.Type = ReservedTypes.Int;
                    break;

                case "double":
                case "dbl":
                    typeExpression.Type = ReservedTypes.Double;
                    break;

                case "long":
                case "i64":
                    typeExpression.Type = ReservedTypes.Long;
                    break;

                case "boolean":
                case "bool":
                    typeExpression.Type = ReservedTypes.Bool;
                    break;

                case "void":
                    typeExpression.Type = ReservedTypes.Void;
                    break;

                default:
                    return false;
            }

            TokenReader.Consume();
            return true;
        }

        bool ParseArrayExpression(out ArrayExpression arrayExpression)
        {
            arrayExpression = new ArrayExpression();
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
            lengthExpression = new LengthExpression();

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
                    throw new Exception("Expression expected after length symbol");
                }
            }

            return false;
        }

        bool ParseParenthesizedExpression(out ParenthesizedExpression parenthesizedExpression)
        {
            parenthesizedExpression = new ParenthesizedExpression();

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
                    throw new Exception("Expression expected in parenthesized expression");
                }
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
            expression = new Expression();

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


            return false;
        }


        // -- Complex Expressions -- //

        bool ParseCallExpression(Expression expression, out CallExpression callExpression)
        {
            callExpression = new CallExpression()
            {
                Expression = expression
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
                Left = leftExpression
            };

            LexKinds arithmeticKind = TokenReader.Peek().Kind;
            switch (arithmeticKind)
            {
                case LexKinds.Addition:
                case LexKinds.Subtraction:
                case LexKinds.Multiplication:
                case LexKinds.Division:
                case LexKinds.Modulus:
                case LexKinds.Exponential:
                    arithmeticExpression.Operator = (ArithmeticOperator)arithmeticKind;
                    break;

                default:
                    return false;
            }

            TokenReader.Consume();

            if (ParseExpression(out Expression rightExpression))
            {
                arithmeticExpression.Right = rightExpression;
                return true;
            }

            throw new Exception("Invalid right hand expression when parsing arithemtic expression");
        }

        bool ParseRangeExpression(Expression leftExpression, out RangeExpression rangeExpression)
        {
            rangeExpression = new RangeExpression()
            {
                Start = leftExpression
            };

            if (TokenReader.Expect(LexKinds.Range))
            {
                TokenReader.Skip(1);
                if (ParseExpression(out Expression rightExpression))
                {
                    rangeExpression.End = rightExpression;
                    return true;
                }
                throw new Exception("Invalid right hand expression when parsing range expression");
            }
            return false;
        }

        bool ParseMemberExpression(Expression parentExpression, out MemberExpression memberExpression)
        {
            memberExpression = new MemberExpression()
            {
                Parent = parentExpression
            };

            if (TokenReader.Expect(LexKinds.Dot))
            {
                TokenReader.Consume();
                if (ParseExpression(out Expression childExpression))
                {
                    memberExpression.Member = childExpression;
                    return true;
                }
                throw new Exception("Invalid member expression, expected an identifier after the dot");
            }
            return false;
        }

        bool ParseIndexExpression(Expression expression, out IndexExpression indexExpression)
        {
            indexExpression = new IndexExpression()
            {
                Expression = expression
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
                Left = leftExpression
            };

            if(TokenReader.Expect(LexKinds.Subtraction) && TokenReader.Expect(LexKinds.ChevronClose, 1))
            {
                TokenReader.Skip(2);
                if (ParseExpression(out Expression rightExpression))
                {
                    unpackExpression.Right = rightExpression;
                    return true;
                }
                throw new Exception("Invalid right hand expression when parsing unpack expression");
            }
            return false;
        }

        bool ParseRelationalExpression(Expression leftExpression, out RelationalExpression relationalExpression)
        {
            relationalExpression = new RelationalExpression();
            relationalExpression.Left = leftExpression;

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
                            case LexKinds.Equals: relationalExpression.Operator = RelationalOperators.EqualTo; break;
                            case LexKinds.NotEqualTo: relationalExpression.Operator = RelationalOperators.NotEqualTo; break;
                            case LexKinds.BiggerOrEqual: relationalExpression.Operator = RelationalOperators.BiggerOrEqual; break;
                            case LexKinds.SmallerOrEqual: relationalExpression.Operator = RelationalOperators.SmallerOrEqual; break;
                            case LexKinds.ChevronOpen: relationalExpression.Operator = RelationalOperators.SmallerThan; break;
                            case LexKinds.ChevronClose: relationalExpression.Operator = RelationalOperators.BiggerThan; break;
                        }

                        TokenReader.Skip(1);
                        if (ParseExpression(out Expression right))
                        {
                            relationalExpression.Right = right;
                            return true;
                        }
                        throw new Exception("Invalid right hand expression when parsing relational expression");
                    }
            }

            return false;
        }

        bool ParseParameterExpression(out ParameterExpression parameterExpression)
        {
            parameterExpression = new ParameterExpression();

            if(ParseTypeExpression(out TypeExpression typeExpression))
            {
                parameterExpression.Type = typeExpression;
            }
            else
            {
                if (ParseIdentifierExpression(out IdentifierExpression identifierExpression))
                {
                    if (ParseMemberExpression(identifierExpression, out MemberExpression memberExpression))
                    {
                        parameterExpression.Type = memberExpression;
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
                throw new Exception("Invalid parameter expression, expected an identifier after the type expression");
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

        bool ParseParameterExpressions(out List<ParameterExpression> parameters)
        {
            parameters = new List<ParameterExpression>();

            for (; ; )
            {
                if (ParseParameterExpression(out ParameterExpression parameter))
                {
                    parameters.Add(parameter);
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
                Initializer = indexExpression
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
            notExpression = new NotExpression();

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
                    throw new Exception("Expression expected when parsing not expression");
                }
            }

            return false;
        }

        bool ParseNegateExpression(out NegateExpression negateExpression)
        {
            negateExpression = new NegateExpression();

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
                    throw new Exception("Expression expected when parsing negate expression");
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
                            if (ParseAssignmentStatement(assignmentVariableExpression, out AssignmentStatement assignmentStatement))
                            {
                                objectInitializerExpression.PropertyAssignments.Add(assignmentStatement);
                                continue;
                            }
                            else
                            {
                                throw new Exception($"Line {TokenReader.Peek().Line}: Property assignment in object initializer is invalid.");
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

        bool ParseExpression(out Expression expression, bool parseSimple = false)
        {
            expression = new Expression();

            if (ParseSimpleExpression(out Expression simpleExpression))
            {
                expression = simpleExpression;
            }
            else
            {
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
            closureStatement = new ClosureStatement();

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
                Expression = expression
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

        bool ParseFunctionDeclarationStatement(Expression typeExpression, IdentifierExpression nameExpression, out FunctionDeclarationStatement functionDeclarationStatement)
        {
            functionDeclarationStatement = new FunctionDeclarationStatement() 
            {
                ReturnType = typeExpression
            };

            functionDeclarationStatement.Name = nameExpression.Name;

            if (TokenReader.Expect(LexKinds.ParentheseOpen))
            {
                TokenReader.Consume();
                if (ParseParameterExpressions(out List<ParameterExpression> parameters))
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

            return false;
        }

        bool ParseVariableDeclarationStatement(Expression typeExpression, IdentifierExpression nameExpression, out VariableDeclarationStatement variableDeclarationStatement)
        {
            variableDeclarationStatement = new VariableDeclarationStatement()
            {
                Type = typeExpression,
                Name = nameExpression.Name
            };
            if (TokenReader.Expect(LexKinds.Equals))
            {
                TokenReader.Consume();
                if (ParseExpression(out Expression valueExpression))
                {
                    variableDeclarationStatement.Expression = valueExpression;
                    return true;
                }
                throw new Exception($"Line {TokenReader.Peek().Line}: Invalid variable declaration statement");
            }

            return true;
        }

        bool ParseIfStatement(out IfStatement ifStatement)
        {
            ifStatement = new IfStatement();

            if (TokenReader.Expect(LexKinds.Keyword, "if"))
            {
                TokenReader.Skip(1);

                if (ParseExpression(out Expression conditionExpression))
                {
                    ifStatement.Condition = conditionExpression;
                }
                else
                {
                    throw new Exception($"Line {TokenReader.Peek().Line}: Invalid if statement, expected a condition expression");
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
                    Console.WriteLine("Parsing elseif...");
                    if(TokenReader.Expect(LexKinds.Keyword, "elseif"))
                    {
                        Console.WriteLine("Started parsing of elseif...");
                        IfStatement elseIfStatement = new IfStatement();

                        TokenReader.Skip(1);
                        if (ParseExpression(out Expression elseIfConditionExpression))
                        {
                            elseIfStatement.Condition = elseIfConditionExpression;
                        }
                        else
                        {
                            throw new Exception($"Line {TokenReader.Peek().Line}: Invalid elseif statement, expected a condition expression");
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
                                throw new Exception($"Line {TokenReader.Peek().Line}: Invalid elseif statement, expected a complete closure");
                            }

                            continue;
                        }
                        else if (ParseStatement(out Statement elseIfStatementBody))
                        {
                            elseIfStatement.Body.Statements.Add(elseIfStatementBody);
                            ifStatement.ElseIfs.Add(elseIfStatement);
                            continue;
                        }


                        throw new Exception($"Line {TokenReader.Peek().Line}: Invalid elseif statement, could not parse elseif");
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
            assignmentStatement = new AssignmentStatement()
            {
                Variable = variable
            };

            if (TokenReader.Expect(LexKinds.Equals))
            {
                TokenReader.Consume();
                if (ParseExpression(out Expression valueExpression))
                {
                    assignmentStatement.Expression = valueExpression;
                    return true;
                }
                throw new Exception($"Line {TokenReader.Peek().Line}: Invalid assignment statement, expected an expression after the equals sign");
            }
            return false;
        }

        bool ParseForStatement(out ForStatement forStatement)
        {
            forStatement = new ForStatement();
            if (TokenReader.Expect(LexKinds.Keyword, "for"))
            {
                TokenReader.Skip(1);

                Console.WriteLine($"------ {TokenReader.Peek().Kind}, {TokenReader.Peek().Value}");
                if (ParseExpression(out Expression expression))
                {
                    if (expression is RangeExpression rangeExpression)
                    {
                        forStatement.Range = rangeExpression;
                    }
                    else
                    {
                        throw new Exception($"Line {TokenReader.Peek().Line}: Invalid for statement, expected a range expression after the 'for' keyword, got {expression.GetType().FullName} ({TokenReader.Peek().Kind}, {TokenReader.Peek().Value})");
                    }
                }
                else
                {
                    throw new Exception($"Line {TokenReader.Peek().Line}: Invalid for statement, expected expression after the 'for' keyword");
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
                        Type = new TypeExpression { Type = ReservedTypes.Int }
                    };
                }
                else
                {
                    throw new Exception($"Line {TokenReader.Peek().Line}: Invalid for statement, expected an identifier after the range expression and comma");
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
            returnStatement = new ReturnStatement();
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
            whileStatement = new WhileStatement();

            if(TokenReader.Expect(LexKinds.Keyword, "while"))
            {
                TokenReader.Skip(1);
                if(ParseExpression(out Expression expression))
                {
                    whileStatement.Expression = expression;
                }
                else
                {
                    throw new Exception("Expression expected after while keyword");
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
            structStatement = new StructStatement();

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

                for(; ; )
                {
                    if(ParseExpression(out Expression expression, true))
                    {
                        if (expression is TypeExpression
                            || expression is IdentifierExpression
                            || expression is MemberExpression)
                        {

                            if (ParseIdentifierExpression(out IdentifierExpression nameExpression))
                            {
                                if (ParseVariableDeclarationStatement(expression, nameExpression, out VariableDeclarationStatement variableDeclarationStatement))
                                {
                                    structStatement.Fields.Add(variableDeclarationStatement);
                                }
                            }
                        }
                        else
                        {
                            throw new Exception($"Line {TokenReader.Peek().Line}: Type / identifier / member expected when parsing field in struct declaration.");
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

        bool ParseStatement(out Statement statement)
        {
            statement = new Statement();

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

            if(ParseExpression(out Expression expression, true))
            {
                if(ParseCallStatement(expression, out CallStatement callStatement))
                {
                    statement = callStatement;
                    return true;
                }

                if(expression is TypeExpression 
                    || expression is IdentifierExpression
                    || expression is MemberExpression)
                {

                    if(ParseIdentifierExpression(out IdentifierExpression nameExpression))
                    {

                        if (ParseFunctionDeclarationStatement(expression, nameExpression, out FunctionDeclarationStatement functionDeclarationStatement))
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
        public StatementList ParseLexTokenList()
        {
            TokenReader.LexTokens.RemoveAll(token => token.Kind == LexKinds.NewLine);

            if (ParseStatements(out StatementList statementList))
            {
                return statementList;
            }

            throw new Exception("Failed to parse the lex token list, is code valid?");
        }
    }
}
