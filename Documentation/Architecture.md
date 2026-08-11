# Architecture
This readme aims to establish the architectural decisions behind Common C.

This can be used to learn, understand how you can improve your own compiler or contribute to the project.

## Pipeline
1. [Lexical analysis](https://github.com/Compiler-Organization/CommonC/blob/master/Lexer/LexicalAnalyser.cs)
2. [Syntax parser](https://github.com/Compiler-Organization/CommonC/blob/master/Parser/SyntaxParser.cs)
3. Semantic analysis
    * [Declaration scope passer](https://github.com/Compiler-Organization/CommonC/blob/master/Semantic/SemanticAnalyzer.cs)
    * [Type tracker](https://github.com/Compiler-Organization/CommonC/blob/master/Semantic/TypeTracker.cs)
4. Code generator
    * [LLVM code generator](https://github.com/Compiler-Organization/CommonC/blob/master/Targets/LLVM/CodeGen/LLVMCodeGen.cs)
    * [CommonIR code generator](https://github.com/Compiler-Organization/CommonC/blob/master/Targets/CommonIR/CodeGen/CommonIRCodeGen.cs)
