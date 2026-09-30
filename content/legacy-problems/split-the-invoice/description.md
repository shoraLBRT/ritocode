# Split the invoice without losing a penny

`InvoiceSplitter.Split` divides an invoice between its payees. It is four months old, it has been
patched twice after a support ticket, and the patches are still visible: two special cases guard the
easy inputs, three guard clauses sit above them, and the division itself is one line that rounds
each share on its own.

It is also wrong. The shares it hands back do not always add up to the invoice. Three of the tests
in `tests/` fail before you change anything, and all three are that one piece of arithmetic seen
from a different side.

## Your task

Make `src/InvoiceSplitter.cs` correct, and make it readable while you are in there.

The rules the tests pin down:

- The shares always sum to exactly the invoice.
- Every share is a whole number of cents.
- No two shares differ by more than one cent.
- Where the invoice does not divide evenly, the spare cents go to the earliest payees.
- An invoice below zero, an invoice that is not a whole number of cents, and a split between fewer
  than one payee are each rejected.

Money that is not a whole number of cents does not exist, which is the hint: a calculation that
rounds at the end has already lost the thing it needed to account for.

## What is graded

- The project compiles with warnings treated as errors.
- Every test in `tests/` passes, unchanged.

The tests are read-only. Changing one so that it agrees with the code is not a fix, and the grader
restores them from the package before it runs anything.

## What is not graded

Whether you keep one method or five, whether the spare cents are handed out in a loop or by index
arithmetic, and what you call things. A reader should be able to find the rounding rule without
reading the guard clauses first; how you get there is yours.
