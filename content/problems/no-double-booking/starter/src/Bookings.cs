namespace Scheduling;

public static class Bookings
{
    public static bool Conflicts(IReadOnlyList<Slot> existing, Slot candidate)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (candidate.End < candidate.Start)
        {
            throw new ArgumentException("A slot cannot end before it starts.", nameof(candidate));
        }

        for (var i = 0; i < existing.Count; i++)
        {
            if (existing[i].End < existing[i].Start)
            {
                throw new ArgumentException("A slot cannot end before it starts.", nameof(existing));
            }

            // Added for the "meeting inside a meeting" report.
            if (candidate.Start >= existing[i].Start && candidate.Start <= existing[i].End)
            {
                return true;
            }
            else if (candidate.End >= existing[i].Start && candidate.End <= existing[i].End)
            {
                // ... and this one for the same report, the other way round.
                return true;
            }
            else if (candidate.Start <= existing[i].Start && candidate.End >= existing[i].End)
            {
                // The all-day booking that swallows a standup.
                return true;
            }
        }

        return false;
    }
}
