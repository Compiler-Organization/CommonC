using CommonC.Lexer.Objects;

namespace CommonC.Lexer
{
    /// <summary>
    /// Generic token reader for the abstract syntax tree generator
    /// </summary>
    public class LexTokenReader
    {
        /// <summary>
        /// The lexical tokens in the token reader
        /// </summary>
        public LexTokenList LexTokens { get; set; }

        /// <summary>
        /// The base of the token reader
        /// </summary>
        public int Base { get; set; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="lexTokens"></param>
        public LexTokenReader(LexTokenList lexTokens)
        {
            this.LexTokens = lexTokens;
        }

        /// <summary>
        /// Peeks into the lextoken list at the base + offset in the token reader
        /// </summary>
        /// <param name="Offset"></param>
        /// <returns></returns>
        public LexToken Peek(int Offset = 0) => LexTokens[Base + Offset];

        /// <summary>
        /// Consumes current lex token and skips forward
        /// </summary>
        /// <returns></returns>
        public LexToken Consume() => LexTokens[Base++];

        /// <summary>
        /// Removes the token from the list
        /// Mainly used in the preprocessing, cautious using outside.
        /// </summary>
        /// <returns></returns>
        public bool Remove() => LexTokens.Remove(Peek());

        /// <summary>
        /// Skils a token
        /// </summary>
        /// <param name="i"></param>
        public void Skip(int i) => Base += i;

        /// <summary>
        /// Expects a lex kind at the current + offset position
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool Expect(LexKinds kind, int offset = 0) => (this.Base + offset <= this.LexTokens.Count - 1 && this.LexTokens[this.Base + offset].Kind == kind);

        /// <summary>
        /// Expects a lex token at the current + offset position
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool Expect(LexKinds kind, string value, int offset = 0) => (this.Base + offset <= this.LexTokens.Count - 1 && this.Base + offset >= 0 && this.LexTokens[this.Base + offset].Kind == kind && this.LexTokens[this.Base + offset].Value == value);

        /// <summary>
        /// Expects a lex kind at the current + offset position, will throw an exception if conditions are not met
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool ExpectFatal(LexKinds kind, int offset = 0) => (this.Base + offset <= this.LexTokens.Count - 1 && this.LexTokens[this.Base + offset].Kind == kind) ? true : throw new Exception($"Line {this.LexTokens[this.Base + offset].Line}: {kind} expected, got {this.LexTokens[this.Base + offset].Kind}, {this.LexTokens[this.Base + offset].Value}");

        /// <summary>
        /// Expects a lex kind at the current + offset position, will throw an exception if conditions are not met
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool ExpectFatal(LexKinds kind, string value, int offset = 0) => (this.Base + offset <= this.LexTokens.Count - 1 && this.LexTokens[this.Base + offset].Kind == kind) && this.LexTokens[this.Base + offset].Value == value ? true : throw new Exception($"Line {this.LexTokens[this.Base + offset].Line}: {kind} and {value} expected, got {this.LexTokens[this.Base + offset].Kind} and {this.LexTokens[this.Base + offset].Value}");

        /// <summary>
        /// Expects a lex kind at the current + offset position, will throw an exception if conditions are not met
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool ExpectFatal(string value, int offset = 0) => (this.Base + offset <= this.LexTokens.Count - 1 && this.LexTokens[this.Base + offset].Value == value) ? true : throw new Exception($"Line {this.LexTokens[this.Base + offset].Line}: {value} expected, got {this.LexTokens[this.Base + offset].Value}({this.LexTokens[this.Base + offset].Kind})");
    }
}
