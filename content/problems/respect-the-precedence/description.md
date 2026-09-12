# Respect the precedence

`Calculator.Evaluate` reads an arithmetic expression and returns its value. It handles precedence,
parentheses, unary minus, decimals and whitespace, and it does all of it in three methods that are
copies of one another with a couple of characters changed — `Sum`, `Product`, `Atom` — each skipping
whitespace again on its own and each reaching into the string by index.

It gets the textbook cases right. `2 + 3 * 4` is 14 and `(2 + 3) * 4` is 20, so the precedence the
name worries about is not the problem.

Two of the tests in `tests/` fail before you change anything — six cases between them — and every
one of them is the same defect seen from a different side: the operators whose answer depends on
which end you start from. Which operators those are is the whole hint.

## Your task

Make `src/Calculator.cs` correct, and leave a grammar a reader can follow.

What the tests pin down:

- Precedence: `*` and `/` bind tighter than `+` and `-`, and parentheses beat both.
- Association: `+`, `-`, `*` and `/` all group left to right. `10 - 3 - 2` is 5, not 9, and
  `100 / 5 / 2` is 10, not 40.
- Unary minus, wherever an operand may appear: at the start, after an operator, and before a
  parenthesis.
- Decimal arithmetic, not binary floating point: `0.1 + 0.2` is exactly `0.3`.
- Whitespace anywhere, including the ends.
- Division by zero raises `DivideByZeroException`; anything the grammar does not accept — an empty
  expression, a trailing operator, an unclosed parenthesis, two numbers in a row — raises
  `FormatException`.

## What is graded

- The project compiles with warnings treated as errors.
- Every test in `tests/` passes, unchanged.

The tests are read-only. Changing one so that it agrees with the code is not a fix, and the grader
restores them from the package before it runs anything.

## What is not graded

Whether you keep recursive descent or build a shunting-yard loop, whether the cursor is a `ref`
parameter, a type of its own or a field, and how you spell the error messages. The duplication
between the three methods is the thing worth removing; what you replace it with is yours.
