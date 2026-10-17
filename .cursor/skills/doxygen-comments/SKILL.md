---
name: doxygen-comments
description: Adds Doxygen-style comments for all functions plus an example of usage. Use when the user asks for Doxygen comments, doxygen style comments, XML/doc comments on every function, or usage examples in comments.
---

# Doxygen comments

Make doxygen style comments for all functions plus example of usage.

## When this applies

- User asks for Doxygen (or “doxyjen”) comments
- Documenting every function in a file or type
- Adding usage examples next to function docs

## Instructions

1. Document **every** function, method, constructor, and local function in the target file(s). Do not skip private helpers.
2. Do not rename existing public APIs unless the user asked to rename. For **new** functions, name them starting with the type of the function, followed by a descriptive name (for example a function that adds two numbers could be named `addNumbers`).
3. Each comment must include:
   - A brief description of what the function does
   - Parameters
   - Return value (use `@return` / `<returns>` even for `void`: state that there is no return value)
   - An example of how to use the function in practice
4. Keep comments next to the function. Do not invent behavior. If a parameter can be null or throw, document that.
5. Prefer the comment syntax of the language in the file:

### C# (this repo)

Use `///` XML tags that Doxygen also understands:

```csharp
/// <summary>
/// Adds two integers and returns the sum.
/// </summary>
/// <param name="left">First addend.</param>
/// <param name="right">Second addend.</param>
/// <returns>The sum of <paramref name="left"/> and <paramref name="right"/>.</returns>
/// <example>
/// <code>
/// int total = addNumbers(2, 3); // 5
/// </code>
/// </example>
public static int addNumbers(int left, int right)
{
    return left + right;
}
```

### C / C++

```c
/**
 * @brief Adds two integers and returns the sum.
 * @param left First addend.
 * @param right Second addend.
 * @return The sum of left and right.
 *
 * @code
 * int total = addNumbers(2, 3); // 5
 * @endcode
 */
int addNumbers(int left, int right);
```

## Workflow

1. Read the file.
2. Add or replace comments on all functions.
3. Keep code behavior unchanged unless the user also asked for a rename/refactor.
4. If a function is too unclear to document, read callers first; do not guess.

## Done when

- Every function in scope has Doxygen-style docs
- Every function has parameters, return, and a usage example
