using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonC.Lexer.Objects
{
    public enum LexKinds
    {
        UN,

        Terminal,

        /// <summary>
        /// E,g '\n'
        /// </summary>
        NewLine,

        /// <summary>
        /// E'g '// Short comment'
        /// </summary>
        Comment,

        /// <summary>
        /// E.g '::'
        /// </summary>
        Cast,

        /// <summary>
        /// E.g '['
        /// </summary>
        BracketOpen,

        /// <summary>
        /// E.g ']'
        /// </summary>
        BracketClose,


        /// <summary>
        /// E.g '('
        /// </summary>
        ParentheseOpen,

        /// <summary>
        /// E.g ')'
        /// </summary>
        ParentheseClose,


        /// <summary>
        /// E.g '{'
        /// </summary>
        BraceOpen,

        /// <summary>
        /// E.g '}'
        /// </summary>
        BraceClose,


        /// <summary>
        /// E.g '<'
        /// </summary>
        ChevronOpen,

        /// <summary>
        /// E.g '>'
        /// </summary>
        ChevronClose,


        /// <summary>
        /// E.g ':'
        /// </summary>
        Colon,

        /// <summary>
        /// E.g ';'
        /// </summary>
        Semicolon,

        /// <summary>
        /// E.g '.'
        /// </summary>
        Dot,

        /// <summary>
        /// E.g ','
        /// </summary>
        Comma,

        /// <summary>
        /// E.g '?'
        /// </summary>
        Question,

        /// <summary>
        /// E.g '!'
        /// </summary>
        Exclamation,

        /// <summary>
        /// E.g '|'
        /// </summary>
        Pipe,

        /// <summary>
        /// E.g '='
        /// </summary>
        Equals,

        /// <summary>
        /// E.g '=='
        /// </summary>
        EqualTo,

        /// <summary>
        /// E.g '~='
        /// </summary>
        NotEqualTo,

        /// <summary>
        /// E.g '>='
        /// </summary>
        BiggerOrEqual,

        /// <summary>
        /// E.g '<='
        /// </summary>
        SmallerOrEqual,

        /// <summary>
        /// E.g '"Hello World"'
        /// </summary>
        String,

        /// <summary>
        /// E.g ''a''
        /// </summary>
        Char,

        /// <summary>
        /// E.g '369'
        /// </summary>
        Number,

        /// <summary>
        /// E.g 'true', 'false'
        /// </summary>
        Boolean,

        /// <summary>
        /// E.g 'var1', '8ball'
        /// </summary>
        Identifier,

        /// <summary>
        /// E.g 'if', 'while'
        /// </summary>
        Keyword,

        /// <summary>
        /// E.g '+'
        /// </summary>
        Addition,

        /// <summary>
        /// E.g '-'
        /// </summary>
        Subtraction,

        /// <summary>
        /// E.g '*'
        /// </summary>
        Multiplication,

        /// <summary>
        /// E.g '/'
        /// </summary>
        Division,

        /// <summary>
        /// E.g '**'
        /// </summary>
        Exponentiation,

        /// <summary>
        /// E.g '%'
        /// </summary>
        Modulus,

        /// <summary>
        /// E.g '^'
        /// </summary>
        Xor,

        /// <summary>
        /// E.g '<<'
        /// </summary>
        LeftShift,

        /// <summary>
        /// E.g '>>'
        /// </summary>
        RightShift,

        /// <summary>
        /// <para>E.g '&amp;'</para>
        /// </summary>
        BitwiseAnd,

        /// <summary>
        /// E.g '|'
        /// </summary>
        BitwiseOr,

        /// <summary>
        /// E.g '..'
        /// </summary>
        Range,

        /// <summary>
        /// E.g '+='
        /// </summary>
        CompoundAdd,

        /// <summary>
        /// E.g '-='
        /// </summary>
        CompoundSub,

        /// <summary>
        /// E.g '*='
        /// </summary>
        CompoundMul,

        /// <summary>
        /// E.g '/='
        /// </summary>
        CompoundDiv,

        /// <summary>
        /// E.g '%='
        /// </summary>
        CompoundMod,

        /// <summary>
        /// E.g '**='
        /// </summary>
        CompoundExp,

        /// <summary>
        /// E.g '^='
        /// </summary>
        CompoundXor,

        /// <summary>
        /// E.g '<<='
        /// </summary>
        CompoundLeftShift, 
        
        /// <summary>
        /// E.g '>>='
        /// </summary>
        CompoundRightShift,

        /// <summary>
        /// E.g '&amp;='
        /// </summary>
        CompoundBitwiseAnd,

        /// <summary>
        /// E.g '|='
        /// </summary>
        CompoundBitwiseOr,

        /// <summary>
        /// E.g 'and'
        /// </summary>
        LogicalAnd,

        /// <summary>
        /// E.g 'or'
        /// </summary>
        LogicalOr,

        /// <summary>
        /// E.g '...'
        /// </summary>
        Vararg,

        /// <summary>
        /// E.g 'T'
        /// </summary>
        GenericType,

        /// <summary>
        /// E.g '#'
        /// </summary>
        Hashtag,

        /// <summary>
        /// End of file
        /// </summary>
        EOF
    }
}
