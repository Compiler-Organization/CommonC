# Common C introductory guide
Welcome to Common C!

## Naming
Standards for naming in Common C.

### Generic
Everything should follow PascalCase, EXCEPT user objects declared within a function scope, which should then follow camelCase.

**Example**
```rust
i32 GlobalVariable = 50

i32 Main() {
    i32 localVariable = 50

    return GlobalVariable + localVariable
}
```

### Libraries
* Function names exclusive to a library that users should not interact with should have two underscores prepended.
    * Example: `Random.coc` -> `fn __GenerateRandomBytes(..`