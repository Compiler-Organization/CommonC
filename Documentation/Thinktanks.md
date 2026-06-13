# Think tanks
This is a document to reflect about and address fundamental problems with the language.

## Liveness
If a variable (n2) is assigned another variable (n1) as a reference, when should n2 be freed?
```cs
struct Person {
    str Name,
    i32 Age
}

fn CreatePerson() {
    Person person = Person {
        Name: "John",
        Age: 50
    }

    // person holds the reference, so freeing person here would invalidate person2, and in return turn it into a null pointer.

    Person person2 = person
    return person2
}

fn main() {
    Person person = CreatePerson()
    printl(person.Name)
    // case 1: person is freed in CreatePerson, output: null
    // case 2: person is maintained since person2 references it, output: John
}
```
___

If a variable can be conditionally assigned different values, when should the variable be freed? What if a value is conditionally assigned, but else not assigned?
* Should variables be nullable at all?
* Should creating variables require a default value assigned?

```cs
struct Person {
    str Name,
    i32 Age
}

fn main() {
    bool isOld = // ... user defined input which cannot be compiled ahead of time.
    Person person

    if (isOld) {
        person = Person {
            Name: "John",
            Age: 80
        }

        printl(person.Name)
    }

    printl(person.Name)
}
```
**Solution**: Determine the last access to the variable, and free then. As usual, if the reference exits the scope, determine when it is last used there.

I am a strong believer that variables should never be null, and therefore should never be initialized to null. Null itself should be supported to maintain compatibility with other libraries, though this should be warned to the user before compiling.

___

If a variable is passed to a function as a reference, when should it be freed?
* That if the function assigns a property of the variable conditionally? When should the property be freed? When should the variable be freed?
* Assuming variables cannot be null, the same "free on last use" principle follows.

___

How should circular references be handled?
* If reference n1 and reference n2 are circular, should n2 be disconnected from n1 before n1 is freed? Will this create a dead pointer? Perhaps determine if n2 is ever used at a later point, and free it at that point?
* n1 holds a pointer to n2, and n2 holds a pointer to n1, meaning it will be safe to free n1 without destroying n2.

```cs
struct Node {
    str Name,
    Node Next
}

Node run_tests() {
    Node n1
    Node n2

    n1 = Node {
        Name: "Node A",
        Next: null
    }

    n2 = Node {
        Name: "Node B",
        Next: null
    }

    n1.Next = n2
    n2.Next = n1

    return n1
}

fn main() {
    Node node = run_tests()

    printl(node.Next.Name)

    // When should n1 and n2 be freed?
}
```