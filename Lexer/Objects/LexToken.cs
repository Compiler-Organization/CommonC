using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonC.Lexer.Objects
{
    public class LexToken
    {
        /// <summary>
        /// Gets / sets the kind of the lex token
        /// </summary>
        public LexKinds Kind { get; set; }

        /// <summary>
        /// Gets / sets the value of the lex token
        /// </summary>
        public string Value { get; set; } = "";

        /// <summary>
        /// Line info
        /// </summary>
        public ulong Line { get; set; }

        /// <summary>
        /// The index of the token on the current line.
        /// </summary>
        public ulong IndexInLine { get; set; }
    }
}
