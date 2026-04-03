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

        bool ParseCallExpression(Expression expression, out CallExpression callExpression)
        {
            callExpression = new CallExpression()
            {
                Expression = expression
            };

            if(TokenReader.Expect(LexKinds.ParentheseOpen))
            {
                TokenReader.Consume();
                if (ParseExpressions(out ExpressionList expressionList))
                {
                    callExpression.Arguments = expressionList;
                }

                if(TokenReader.ExpectFatal(LexKinds.ParentheseClose))
                {
                    TokenReader.Consume();
                    return true;
                }
            }
            return false;
        }

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
                    typeExpression.Type = ReservedTypes.Int;
                    break;

                case "boolean":
                case "bool":
                    typeExpression.Type = ReservedTypes.Bool;
                    break;

                default:
                    return false;
            }

            TokenReader.Consume();
            return true;
        } 

        /// <summary>
        /// Parses expressions without a right hand side
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        bool ParseSimpleExpression(out Expression expression)
        {
            expression = new Expression();

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

            return false;
        }

        bool ParseExpression(out Expression expression)
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
                if (ParseCallExpression(expression, out CallExpression callExpression))
                {
                    expression = callExpression;
                    continue;
                }

                break;
            }

            return true;
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

        bool ParseFunctionDeclarationStatement(Expression typeExpression, out FunctionDeclarationStatement functionDeclarationStatement)
        {
            functionDeclarationStatement = new FunctionDeclarationStatement() 
            {
                ReturnType = typeExpression
            };

            if(TokenReader.Expect(LexKinds.Identifier))
            {
                functionDeclarationStatement.Name = TokenReader.Consume().Value;

                if(TokenReader.Expect(LexKinds.ParentheseOpen))
                {
                    TokenReader.Consume();
                    if (ParseExpressions(out ExpressionList expressionList))
                    {
                        functionDeclarationStatement.Parameters = expressionList;
                    }

                    if (TokenReader.ExpectFatal(LexKinds.ParentheseClose))
                    {
                        TokenReader.Consume();
                    }

                    if(ParseClosureStatement(out ClosureStatement closureStatement))
                    {
                        functionDeclarationStatement.Body = closureStatement;
                        return true;
                    }

                    throw new Exception($"Line {TokenReader.Peek().Line}: Invalid function declaration statement");
                }
            }


            return false;
        }

        bool ParseStatement(out Statement statement)
        {
            statement = new Statement();

            if(ParseSimpleExpression(out Expression expression))
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
                    if (ParseFunctionDeclarationStatement(expression, out FunctionDeclarationStatement functionDeclarationStatement))
                    {
                        statement = functionDeclarationStatement;
                        return true;
                    }
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
