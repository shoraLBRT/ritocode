namespace Scheduling;

/// <summary>
/// The plausible wrong answer: the chain is gone, the validation happens before any comparison, and
/// the boundary is right — but overlap is still decided by asking where the candidate's two ends
/// fall. A candidate that swallows a slot whole has neither end inside it.
/// </summary>
public static class Bookings
{
    public static bool Conflicts(IReadOnlyList<Slot> existing, Slot candidate)
    {
        ArgumentNullException.ThrowIfNull(existing);

        RequireForwardSlot(candidate, nameof(candidate));

        foreach (var slot in existing)
        {
            RequireForwardSlot(slot, nameof(existing));
        }

        return existing.Any(slot => Overlaps(slot, candidate));
    }

    private static bool Overlaps(Slot slot, Slot candidate)
    {
        if (candidate.Start == candidate.End)
        {
            return candidate.Start > slot.Start && candidate.Start < slot.End;
        }

        var startsInside = candidate.Start >= slot.Start && candidate.Start < slot.End;
        var endsInside = candidate.End > slot.Start && candidate.End <= slot.End;

        return startsInside || endsInside;
    }

    private static void RequireForwardSlot(Slot slot, string parameterName)
    {
        if (slot.End < slot.Start)
        {
            throw new ArgumentException("A slot cannot end before it starts.", parameterName);
        }
    }
}
