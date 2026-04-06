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
## Current capabilities
Common C can currently run code like fibonacci and factorial functions as shown below:
```c
int fibonacci(int n) {
	int a = 0
	int b = 1
	for 0..n, i {
		int temp = a
		a = b
		b = temp + a
	}

	return a
}

int factorial(int n)
{
	if n <= 1 {
		return 1
	}
	else {
		return n * factorial(n - 1)
	}
}

int main() {
	int var = 10

	logstr("Fibonacci: ")
	logint(fibonacci(var))
	
	logstr("Factorial: ")
	logint(factorial(var))
}
```

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