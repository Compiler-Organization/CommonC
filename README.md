# Common C
The common C langugage is developed to deliver the performance of natively compiled languages whilst maintaining the ease of memory management without workarounds like garbage collection and borrow checkers.
Common C is object oriented and statically typed.

Common C will be targeting both x86_64 for Intel and AMD aswell as .NET (CIL).

Currently the .NET target is under development, with the x86_64 target to be developed as soon as the .NET target is satisfactory.

> Contributions are welcome with open arms!
___
## Thoughts behind Common C
Other languages are either easy to write, but perform poorly (E.g Python, JavaScript, etc) or they perform well but are difficult to write (E.g C, C++, Rust, etc). Common C is designed to be easy to write and perform well without workarounds like garbage collection and borrow checkers.

The philosophy behind the syntax is to be easily readable and writable. Syntax is developed so that the shortest combination of keystrokes produces functionality. We keep in mind that not every developed posesses giant hands that reach across the keyboard.

An example of this is using `log` as the standard output function. C# uses Console.WriteLine and Rust uses println!. Both of these are long to write and require more keystrokes than simply `log`.

___
## "New" functionality
Unpacking tables has never been easier! Here is an example of unpacking a table in Common C:
```c
// array = { "Hello", "there", "world!", "I'm", "a", "machine", "with", "finite", "possibilities!" }

log(array->3..5)

// I'm a machine
```
Here we are unpacking the array from index 3 to index 5 and logging it to the console.
___

# Language design
Common C uses top-level, global and public declarations for functions, structs and globals - meaning everything can be accessed from anywhere. Given CommonC's ergonomic style, it has been decided that having access to everything anywhere is the "free-est" way of programming. You do not have to declare the visibility of user-types like functions and globals. The only exception is uninitialized user-types like struct declarations.

There really is no point in using `var` or equal in a modern, statically typed language apart from extra syntax clutter - hence the type is directly stated instead.

**Table of contents**
* Expressions
    * [String](#String)
        * Primitive type **string**.
    * [Boolean](#Boolean)
        * Primitive type **boolean**.
    * [Number](#Number)
        * Primitive type **number**.
    * [Identifier](#Identifier)
        * Generic identifiers.
    * [Array](#Array)
        * Unbound, typeless array.
    * [Array initializer](#Array-initializer)
        * Bound, typed array initializer.
    * [Index](#Index)
        * Index of array.
    * [Length](#Length)
        * Length of array.
    * [Range](#Range)
        * Range between two expressions.
    * [Call](#Call)
        * Function call.
    * [Member](#Member)
        * Parent / member relationship.
    * [Relational](#Relational)
        * Equals.
        * Not equals.
        * Greater than.
        * Greater than or equals.
        * Less than.
        * Less than or equals.
    * [Arithmetic](#Arithmetic)
        * Addition.
        * Subtraction.
        * Multiplication.
        * Division.
        * Modulus.
        * Power.
    * [Negate](#Negate)
        * Negate expression from positive to negative.
    * [Not](#Not)
        * Reverse boolean.
    * [Object initializer](#Object-initializer)
        * Bound, typed object initializer.
    * [Parameter](#Parameter)
        * Function declaration parameter.
    * [Parenthesized](#Parenthesized)
        * Parenthesized expression for control flow.
    * [Type](#Type)
        * Native, reserved types.
    * [Unpack](#Unpack)
        * Unpacking arrays.
* Statements
    * [Assignment](#Assignment)
        * Variable assignments.
    * [Function call](#Function-call)
        * Declared function call.
    * [Closure](#Closure)
        * Closure to determine control flow.
    * [For loop](#For-loop)
        * Conditional, numeric loop.
    * [Function declaration](#function-declaration)
        * Top-level function declaration.
    * [Return](#Return)
        * Returns a function.
    * [Struct](#Struct)
        * Structural user-type.
    * [Variable declaration](#Variable-declaration)
        * Local and global declaration.
    * [While](#While)
        * Conditional loop.
    * [If](#If)
        * Conditional control flow.

## Expressions

## Statements

### Call
Syntax
```
<Simple>(<Expression>..)
```

Example
```
log("Hello, world!")
```

### Function declaration
Syntax
```
<Type | Identifier | Member> <Identifier>(<Parameters>) {
	<Statements>
}
```

Example
```
str main() {
	log("Hello, World!");
}
```