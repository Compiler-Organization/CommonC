# Common C introductory guide
Welcome to Common C!

## Naming
Standards for naming in Common C.

### Generic
Everything should follow PascalCase, EXCEPT user objects declared within a function scope, which should then follow camelCase.

### Libraries
* Function names users are interacting with should always contain the library name prepended, seperated by an underscore.
    * Example: `Random.coc` -> `i32 Random_Next(..`
* Function names exclusive to a library that users should not interact with should have two underscores prepended.
    * Example: `Random.coc` -> `fn __GenerateRandomBytes(..`