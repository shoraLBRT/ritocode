# Stop the double bookings

`Bookings.Conflicts` answers one question — may this slot be added to a diary that already holds
these? — and it answers it with a chain of four comparisons that were each added to fix a different
report. Read together, they disagree about what happens at the single instant where one slot ends
and the next begins.

Five of the tests in `tests/` fail before you change anything. Four of them are about that instant.
The fifth is about something less obvious: the loop rejects a malformed slot only if it reaches one,
so a diary whose first entry already conflicts never gets as far as noticing that its second entry
ends before it starts.

## Your task

Make `src/Bookings.cs` correct, and leave behind something whose answer at a boundary can be read
off rather than traced.

The rules the tests pin down:

- Slots that touch — one ends exactly where the next begins — do not conflict. A 10:00–11:00 meeting
  leaves 11:00 free.
- Any shared stretch of time conflicts, whichever slot contains the other, and an identical slot
  conflicts with itself.
- A zero-length slot conflicts only strictly inside another slot, never at its edges.
- A slot that ends before it starts is rejected, whether it is the candidate or one of the slots
  already in the diary, and whether or not an earlier slot would have conflicted first.

## What is graded

- The project compiles with warnings treated as errors.
- Every test in `tests/` passes, unchanged.

The tests are read-only. Changing one so that it agrees with the code is not a fix, and the grader
restores them from the package before it runs anything.

## What is not graded

How you spell the comparison, whether validation is a guard clause or a separate pass, and whether
`Slot` grows behaviour of its own. Those are all reasonable answers. What is being asked for is that
the next person who has to change the boundary rule can find it in one place.
