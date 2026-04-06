using CommonC.Lexer.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/* This lexer is specified for Lua */

namespace CommonC.Lexer
{
    public class LexicalAnalyser
    {
        readonly List<string> Keywords = new List<string>
        {
            // Types
            "string",
            "str",
            "integer",
            "int",
            "boolean",
            "bool",
            "void",

            // Control flow
            "if",
            "elseif",
            "else",

            // Loops
            "while",
            "for",

            "return",
            "ret"
        };

        string Input { get; set; }

        public LexicalAnalyser(string Input)
        {
            this.Input = Input;
        }

        LexKinds Identify(string Value)
        {
            if (ulong.TryParse(Value, out _))
                return LexKinds.Number;

            if (Value == "false"
                || Value == "true")
                return LexKinds.Boolean;

            if (Keywords.Contains(Value))
                return LexKinds.Keyword;

            return LexKinds.Identifier;
        }

        public LexTokenList Analyze()
        {
            LexTokenList LexTokens = new LexTokenList();
            StringBuilder sb = new StringBuilder();
            int Line = 1;

            for (int i = 0; i < Input.Length; i++)
            {
                LexKinds kind = LexKinds.Terminal;
                string value = "";
                switch (Input[i])
                {
                    case '(': kind = LexKinds.ParentheseOpen; break;
                    case ')': kind = LexKinds.ParentheseClose; break;

                    case '[': kind = LexKinds.BracketOpen; break;
                    case ']': kind = LexKinds.BracketClose; break;

                    case '{': kind = LexKinds.BraceOpen; break;
                    case '}': kind = LexKinds.BraceClose; break;

                    case '<':
                        {
                            if(Input[i + 1] == '=')
                            {
                                kind = LexKinds.SmallerOrEqual;
                                i++;
                            }
                            else
                            {
                                kind = LexKinds.ChevronOpen;
                            }
                            break;
                        }
                    case '>':
                        {
                            if (Input[i + 1] == '=')
                            {
                                kind = LexKinds.BiggerOrEqual;
                                i++;
                            }
                            else
                            {
                                kind = LexKinds.ChevronClose;
                            }
                            break;
                        }

                    case '#': kind = LexKinds.Hashtag; break;

                    case ';': kind = LexKinds.Semicolon; break;

                    case ':':
                        {
                            kind = LexKinds.Colon;
                            if (Input[i + 1] == ':')
                            {
                                kind = LexKinds.Cast;
                                i++;
                            }

                            break;
                        }

                    case '|': kind = LexKinds.Pipe; break;

                    case ',': kind = LexKinds.Comma; break;

                    case '.':
                        {
                            kind = LexKinds.Dot;

                            if(Input[i + 1] == '.')
                            {
                                kind = LexKinds.Range;
                                if (Input[i + 2] == '.')
                                {
                                    kind = LexKinds.Vararg;
                                    i++;
                                }
                                i++;
                            }

                            break;
                        }

                    case '"':
                        {
                            i++;
                            while (Input[i] != '"')
                            {
                                sb.Append(Input[i]);
                                i++;
                            }
                            value = sb.ToString();
                            kind = LexKinds.String;
                            sb.Clear();

                            break;
                        }

                    case '\'':
                        {
                            i++;
                            while (Input[i] != '\'')
                            {
                                if (Input[i] == '\\' && Input[i + 1] == '\'')
                                {
                                    i++;
                                }
                                sb.Append(Input[i]);
                                i++;
                            }
                            value = sb.ToString();
                            kind = LexKinds.String;
                            sb.Clear();

                            break;
                        }

                    case '=':
                        {
                            if(Input[i + 1] == '=')
                            {
                                kind = LexKinds.EqualTo;
                                i++;
                            }
                            else
                            {
                                kind = LexKinds.Equals;
                            }
                            break;
                        }

                    case '~':
                        {
                            if (Input[i + 1] == '=')
                            {
                                kind = LexKinds.NotEqualTo;
                                i++;
                            }
                            break;
                        }


                    case '\n':
                        Line++;
                        kind = LexKinds.NewLine;
                        break;

                    case '?':
                        {
                            kind = LexKinds.Question;
                            break;
                        }

                    case '!':
                        {
                            kind = LexKinds.Exclamation;
                            break;
                        }

                    case '+':
                        {
                            kind = LexKinds.Addition;
                            if (Input[i + 1] == '=')
                            {
                                kind = LexKinds.CompoundAdd;
                                i++;
                            }
                            break;
                        }
                    case '-':
                        {
                            kind = LexKinds.Subtraction;
                            if (Input[i + 1] == '=')
                            {
                                kind = LexKinds.CompoundSub;
                                i++;
                            }
                            break;
                        }
                    case '*':
                        {
                            kind = LexKinds.Multiplication;
                            if (Input[i + 1] == '=')
                            {
                                kind = LexKinds.CompoundMul;
                                i++;
                            }
                            break;
                        }
                    case '/':
                        {
                            
                            kind = LexKinds.Division;
                            if (Input[i + 1] == '=')
                            {
                                kind = LexKinds.CompoundDiv;
                                i++;
                            }
                            break;
                        }
                    case '%':
                        {
                            kind = LexKinds.Modulus;
                            if (Input[i + 1] == '=')
                            {
                                kind = LexKinds.CompoundMod;
                                i++;
                            }
                            break;
                        }
                    case '^':
                        {
                            kind = LexKinds.Exponential;
                            if (Input[i + 1] == '=')
                            {
                                kind = LexKinds.CompoundExp;
                                i++;
                            }
                            break;
                        }

                    // Discard
                    case ' ':
                    case '\r':
                    case '\t':
                        break;

                    default:
                        {
                            if (Char.IsLetterOrDigit(Input[i]))
                            {
                                while (Input.Length > i && Char.IsLetterOrDigit(Input[i]))
                                {
                                    sb.Append(Input[i++]);
                                }
                                i--;
                            }
                            value = sb.ToString();
                            kind = Identify(value);

                            sb.Clear();
                            break;
                        }
                }


                if (kind != LexKinds.Terminal)
                {
                    LexTokens.Add(new LexToken()
                    {
                        Kind = kind,
                        Value = value,
                        Line = Line
                    });
                }
            }

            LexTokens.Add(new LexToken()
            {
                Kind = LexKinds.EOF
            });

            return LexTokens;
        }
    }
}
