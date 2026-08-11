<h1 align="center">The official Common C language compiler</h1>
<p align="center">
  <img src="https://github.com/Draugr-official/Skully/actions/workflows/dotnet.yml/badge.svg">
  <img src="https://img.shields.io/badge/version-0.0.2-blue">
</p>

___

* For performance benchmarks, see [Documentation/Performance](Documentation/Performance.md)
* For current problems, see [Documentation/Thinktanks](Documentation/Thinktanks.md)
* For the handbook, see [Documentation/Handbook](Documentation/Handbook.md)

___

The common C language is developed to deliver the performance of natively compiled languages whilst maintaining the ease of memory management without workarounds like garbage collection and borrow checkers.
Common C is object oriented and statically typed.

Common C will be targeting both LLVM aswell as .NET (CIL).

The language is currently in a prototype stage, meaning everything is subject to change. Opinions from the public are welcome!

> Contributions are welcome with open arms!
___
## Demo
We can now render a mandelbrot fractal! Currently this uses direct GDI externs, but will be ported once a proper graphics library has been developed.

The code can be found [here](https://github.com/Compiler-Organization/CommonC/blob/master/CommonC.App/Samples/Mandelbrot.coc)

<img width="628" height="474" alt="image" src="https://github.com/user-attachments/assets/6ee691a1-6e0b-42cd-b66d-e3272a69d4f4" />

___
## Thoughts behind Common C
Other languages are either easy to write, but perform poorly (E.g Python, JavaScript, etc) or they perform well but are difficult to write (E.g C, C++, Rust, etc). Common C is designed to be easy to write and perform well without workarounds like garbage collection and borrow checkers.

The philosophy behind the syntax is to be easily readable and writable. Syntax is developed so that the shortest combination of keystrokes produces functionality whilst maintaining readability. We keep in mind that not every developed posesses giant hands that reach across the keyboard.

An example of this is using `print` as the standard output function. C# uses Console.WriteLine and Rust uses println!. Both of these are long to write and require more keystrokes than simply `print`.

The grand wish of Common C is to be a language where JavaScript, C#, Lua and developers of other garbage-collected languages can write low-level code without needing to relearn an entirely new language and their mental model. The late-stage of Common C will be a heterogeneous compiler with, for example, an integrated array language interopable and indifferent to the "main" language.

At some point you will be able to use .NET libraries aswell as C (or equal) libraries in the same project, as everything will be lifted back to AST before being lowered to the specified target (LLVM or CIL).

Could Common C at some point be the universally compatible language that can be used anywhere without rewriting code? That is the dream.

___

# Language design
Common C uses top-level, global and public declarations for functions, structs and globals - meaning everything can be accessed from anywhere. Given CommonC's ergonomic style, it has been decided that having access to everything anywhere is the "free-est" way of programming. You do not have to declare the visibility of user-types like functions and globals. The only exception is uninitialized user-types like struct declarations.

There really is no point in using `var` or equal in a modern language apart from extra syntax clutter and "type confusion" - hence the choice of the language being statically typed.

Semicolon after statements is optional simply because some people prefer it, though it has no real function during compilation and is ignored.

## print and printl
The standard output function is `print` and `printl`, which writes to console, with printl appending a newline at the end.

These functions wrap printf and takes any input, seperated by commas, determines the type of the input and formats it accordingly.

Example
```cs
printl("String: ", "Hello world!", ", Number: ", 123, ", Boolean: ", true)
// String: Hello world!, Number: 123, Boolean: true
```
