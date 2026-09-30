namespace Scheduling;

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

    /// <summary>
    /// Two stretches of time overlap when each begins before the other ends. Touching slots fail
    /// that on one side, which is why the boundary needs no case of its own.
    /// </summary>
    private static bool Overlaps(Slot slot, Slot candidate)
    {
        if (candidate.Start == candidate.End)
        {
            return candidate.Start > slot.Start && candidate.Start < slot.End;
        }

        return candidate.Start < slot.End && slot.Start < candidate.End;
    }

    private static void RequireForwardSlot(Slot slot, string parameterName)
    {
        if (slot.End < slot.Start)
        {
            throw new ArgumentException("A slot cannot end before it starts.", parameterName);
        }
    }
}
