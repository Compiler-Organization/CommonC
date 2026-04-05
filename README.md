# Common C
The common C langugage is developed to deliver the performance of natively compiled languages whilst maintaining the ease of memory management without workarounds like garbage collection and borrow checkers.
Common C is object oriented, statically typed, 

# Syntax
Simple Expression = Expressions without a right hand side (E.g identifiers, indexes, etc).

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