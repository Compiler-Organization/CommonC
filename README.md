<h1 align="center">The official Common C language compiler</h1>
<p align="center">
  <img src="https://github.com/Draugr-official/Skully/actions/workflows/dotnet.yml/badge.svg">
  <img src="https://img.shields.io/badge/version-0.0.2-blue">
</p>

___

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

Semicolon after statements is optional simply because some people prefer it, though it has no function.

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
        * Left shift.
        * Right shift.
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
    * [Use](#Use)
        * Imports file, parses it into an AST and merges it with the main file.

# Expressions
## String
Generic string taking any character. Starts with a quotation mark and terminates with a quotation mark.
```cs
"<any>"
```

Example
```
"Hello, world!"
```

___

## Boolean
Deterministic boolean.
```cs
true / false
```

Example
```
bool isTrue = true
```

___

## Number
Any number of any length.
```
123
```

Example
```cs
person.age = 50
```

___

## Identifier
Generic identifier starting with a letter and containing letters or numbers.
```
someVariable
```

Example
```cs
log(yourName)
```

___

## Array
An array of expression, seperated by a comma.

```
{ <expr[]> }
```

Example
```cs
{ 5, 10, 15 }
```

___

## Array initializer
Initializes a new array with specified type and size. Each item in the initialized array is seperated by a comma.
```
<expr | type>[<expr | number>] {
    <expr[]>
}
```

Example
```cs
int[3] { 5, 10, 15 }
```

___

## Index
Accesses an array through a specified index.
```
<expr | array<any>>[<expr>]
```

Example
```cs
int arr = int[3] { 5, 10, 15 }

log(arr[1])
```

___

## Length
Gets the length of an array.
```
#<expr>
```

Example
```lua
#arr
```

___

## Call
Performs a call on the given function. Arguments are seperated by a comma.
```
<expr>(<expr[]>)
```

Example
```cs
log("Hello, world!")
```

___

## Member
Accesses the member of a parent. Parent and member are seperated by a dot. Can be nested.
```
<expr>.<expr>
```

Example
```cs
Parent.member
```

___

## Relational
Determines wether two expressions meet a conditional and their relation to eachother.

**Supported operators**
* `==` - Equals.
* `!=` - Not equals.
* `>` - Greater than.
* `>=` - Greater than or equals.
* `<` - Less than.
* `<=` - Less than or equals.
```
<expr> <operator> <expr>
```

Example
```cs
4 > 2
```

___

## Arithmetic
Performs an arithmetic operation on two expressions.

**Supported operators**
* `+` - Addition.
* `-` - Subtraction.
* `*` - Multiplication.
* `/` - Division.
* `^` - Power.
* `%` - Modulus.
```
<expr> <operator> <expr>
```

Example
```cs
2 + 2
```

___

## Negate
Changes an expression to its additive inverse.
```
-<expr>
```

Example
```cs
-100
```

___

## Not
Changes a number or boolean to its binary counterpart.
```
!<expr>
```

Example
```
!true
```

___

## Object initializer
Initializes a new object of a specified type. Properties of the object can be assigned, with name and value seperated by a colon and each property seperated by a comma.
```
<expr | type> {
    <expr>: <expr>,
}
```

Example
```
Person {
    name: "Kai",
    age: 26
}
```

___

## Parameter
The parameter structure used in function declarations.
```
<expr | type> <expr>
```

Example
```
str name
```

___

## Parenthesized
Wraps an expression in parentheses to override syntax precedence.
```
(<expr>)
```

Example
```
(2 + 2) * 5
```

___

## Type
Primitive reserved types.

**Supported types**
* `str` | `string`
* `i8`
* `u8`
* `i16`
* `u16`
* `i32`
* `u32`
* `i64`
* `u64`
* `i128`
* `u128`
* `f32`
* `f64`
* `bool`
* `fn`
```
<type>
```

Example
```rust
i32
```

___

## Unpack
Unpacks an array into seperate components.
```
<expr> | array<any>> -> <expr | range>
```

Example
```cs
arr -> 0..5
```


# Statements

## Assignment
Assigns an expression to a local, global or property
```
<expr> = <expr>
```

Example
```cs
message = "Hello!"
```

___

## Function call
Calls a function and, if the return type is not `fn`, returns it.
```
<expr>(<expr>)
```

Example
```cs
log("Hello, world!")
```

___

## Closure
Creates a new closure for control flow purposes.
```
{ <statements> }
```

Example
```
{ log("Hello, world!") }
```

___

## For loop
Repeats a closure until numeric range has been nivelled. Sets the current iteration to a user-specified local.
```
for <expr | range>, <expr | identifier> {
    <statements>
}
```

Example
```cs
for 0..5, i {
    log(i)
}
```

___

## Function declaration
Declares a new function with parameters and a return type. Parentheses for parameters is not needed if there are none.
```
<expr | type> <expr | identifier>(<parameters>) {
    <statements>
}
```

Example
```rust
fn testFunc(str message) {
    log(message)
}

fn main {
    testFunc("Hello, world!")
}
```

___

## Return
Returns an expression and exits the function immediately.
```
return <expr>
```

Example
```cs
return true
```

___

## Struct
Declares a new struct object with user-defined properties. Properties are seperated by a comma and cannot be assigned a default value.
```
struct <expr | identifier> {
    <expr | type> <expr | identifier>,
}
```

Example
```rust
struct Person {
    str name,
    i32 age
}
```

___

## Variable declaration
Declares a new variable. If the declaration is in a closure, it will become a local. If the variable is declared on the global scope, it will become a global. Declarations do not require a default value.
```
<expr | type> <expr | identifier> = <expr>
```

Example
```cs
str message = "Hello, world!"
```

___

## While
Repeats a closure until the condition is met.
```
while <expr> {
    <statements>
}
```

Example
```cs
while true {
    log("To infinity...")
}
```

___

## If
Branches to closure if condition is met. If main condition is not met, checks elseifs and lastly the else case.
```
if <expr> {
    <statements>
}
elseif <expr> {
    <statements>
}
else {
    <statements>
}
```

Example
```cs
if 2 == 2 {
    log(4)
}
```

___

## Use
Adds a .coc extension, imports the file from the name in the specified working directory, parses its syntax and merges it with the main file.
```
use <expr | identifier>
```

Example
```rust
use math
```