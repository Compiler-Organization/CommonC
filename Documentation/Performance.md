# Performance
The current iteration of Common C proves to be more performant than nearly any other language.

This was the result of each benchmark:
| Language              | Performance (lower is better) | Time    |
| --------------------- | ----------------------------- | ------- |
| [Common C](#common-c) | 1x                            | 3,413s  |
| [C](#c)               | 1.14x                         | 3.884s  |
| [C++](#c++)           | 1.51x                         | 5.142s  |
| [Rust](#rust)         | 3x                            | 10.512s |
| [Python](#python)     | 51.5x                         | 175.73s |

Shown below is the specifications for each benchmark.

| Language | CPU               | RAM            | OS         | Compiler/Interpreter                                                                             | Optimization    |
| -------- | ----------------- | -------------- | ---------- | ------------------------------------------------------------------------------------------------ | --------------- |
| Common C | AMD Ryzen 7 3700X | 48GB DDR4-3000 | Windows 11 | Common C 1.0                                                                                     | No optimization |
| C        | AMD Ryzen 7 3700X | 48GB DDR4-3000 | Windows 11 | clang version 20.1.8, i686-pc-windows-msvc, posix                                                | -O3             |
| C++      | AMD Ryzen 7 3700X | 48GB DDR4-3000 | Windows 11 | Microsoft (R) C/C++ Optimizing Compiler Version 19.50.35730 for x86                              | /O2             |
| Rust     | AMD Ryzen 7 3700X | 48GB DDR4-3000 | Windows 11 | rustc 1.67.1 (d5a82bbd2 2023-02-07)                                                              | -C opt-level=3  |
| Python   | AMD Ryzen 7 3700X | 48GB DDR4-3000 | Windows 11 | Python 3.14.4 (tags/v3.14.4:23116f9, Apr  7 2026, 14:10:54) [MSC v.1944 64 bit (AMD64)] on win32 | N/A             |

# Common C
The Common C implementation of the recursive fibonacci function is as follows:
```c
i32 fib(i32 n) {
    if n < 2
        return n
    return fib(n - 1) + fib(n - 2)
}

fn main() {
    i32 n = 45
    i32 result = fib(n)
    logl("Fibonacci(", n, ") = ", result)
}
```
This application does not use a timer, meaning system overhead like application startup time is included in the benchmark.

Result; 

```
Fibonacci(45) = 1134903170
Execution completed in 3,413s
```

___

# C
The C implementation of the recursive fibonacci function is as follows:
```c
#include <stdio.h>
#include <time.h>

long long fibonacci(int n) {
    if (n <= 1) return n;
    return fibonacci(n - 1) + fibonacci(n - 2);
}

int main() {
    int n = 45; 

    clock_t start = clock();

    long long result = fibonacci(n);

    clock_t end = clock();

    double time_taken = ((double)(end - start)) / CLOCKS_PER_SEC;

    printf("Fibonacci(%d) = %lld\n", n, result);
    printf("Time taken: %f seconds\n", time_taken);

    return 0;
}
```

Result;
```
Fibonacci(45) = 1134903170
Time taken: 3.884000 seconds
```
___

# C++
The C++ implementation of the recursive fibonacci function is as follows:
```cpp
#include <iostream>
#include <chrono>

long long fibonacci(int n) {
    if (n <= 1) return n;
    return fibonacci(n - 1) + fibonacci(n - 2);
}

int main() {
    int n = 45;

    auto start = std::chrono::high_resolution_clock::now();

    long long result = fibonacci(n);

    auto end = std::chrono::high_resolution_clock::now();

    std::chrono::duration<double, std::milli> duration = end - start;

    std::cout << "Fibonacci(" << n << ") = " << result << std::endl;
    std::cout << "Time taken: " << duration.count() << " ms" << std::endl;

    return 0;
}
```

Result;
```
Fibonacci(45) = 1134903170
Time taken: 5142.84 ms
```
___

# Rust
The Rust implementation of the recursive fibonacci function is as follows:
```rust
use std::time::Instant;

fn fibonacci(n: u32) -> u32 {
    match n {
        0 => 0,
        1 => 1,
        _ => fibonacci(n - 1) + fibonacci(n - 2),
    }
}

fn main() {
    let start = Instant::now();
    
    let n = 45;
    let result = fibonacci(n);

    let duration = start.elapsed();
    println!("Fibonacci({}) = {}", n, result);
    println!("Time elapsed: {:?}", duration);
}
```

Result;
```
Fibonacci(45) = 1134903170
Time elapsed: 10.5127339s
```

___

# Python
The Python implementation of the recursive fibonacci function is as follows:
```python
import time

def fibonacci(n):
    """Return the nth Fibonacci number using recursion."""
    if n <= 1:
        return n
    return fibonacci(n - 1) + fibonacci(n - 2)


start = time.time()
result = fibonacci(45)
end = time.time()

print(f"fibonacci(45) = {result}")
print(f"Execution time: {end - start:.2f} seconds")
```

Result;
```
fibonacci(45) = 1134903170
Execution time: 175.73 seconds
```