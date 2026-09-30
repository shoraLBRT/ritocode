# Untangle the order total calculator

`OrderTotal.Calculate` is the only thing standing between a basket and a price, and it has been
edited by six people in a hurry. It computes a subtotal, applies a coupon, works out tax for a
country, decides whether shipping is free, and rounds — all in one method, with the rules written
as magic numbers in the order they were added.

The behaviour is correct. The tests in `tests/` pin it down, and they pass before you change
anything.

## Your task

Refactor `src/OrderTotal.cs` so that each rule is named and separately readable:

- the subtotal,
- the coupon discount,
- the tax for a country,
- the shipping charge,
- the final rounding.

## What is graded

- The project still compiles with warnings treated as errors.
- Every test in `tests/` still passes, unchanged.

The tests are read-only. Making a test agree with a changed calculation is not a refactoring, and
the grader restores them before it runs anything.

## What is not graded

Naming taste, file layout inside `src/`, and whether you use methods, a class per rule, or a table.
Any of those can be the right answer. What is being asked for is that a reader can find one rule
without reading all five.
