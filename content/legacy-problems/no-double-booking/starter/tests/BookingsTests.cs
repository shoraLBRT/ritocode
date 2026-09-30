using System.Globalization;
using Xunit;

namespace Scheduling.Tests;

public sealed class BookingsTests
{
    private static readonly DateTimeOffset Day = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnEmptyDiary_ConflictsWithNothing()
    {
        Assert.False(Bookings.Conflicts([], At("10:00", "11:00")));
    }

    [Fact]
    public void AnOverlappingSlot_Conflicts()
    {
        Assert.True(Bookings.Conflicts([At("10:00", "11:00")], At("10:30", "11:30")));
    }

    [Fact]
    public void ASlotThatStartsWhereAnotherEnds_DoesNotConflict()
    {
        Assert.False(Bookings.Conflicts([At("10:00", "11:00")], At("11:00", "12:00")));
    }

    [Fact]
    public void ASlotThatEndsWhereAnotherStarts_DoesNotConflict()
    {
        Assert.False(Bookings.Conflicts([At("10:00", "11:00")], At("09:00", "10:00")));
    }

    [Fact]
    public void ASlotInsideAnother_Conflicts()
    {
        Assert.True(Bookings.Conflicts([At("10:00", "11:00")], At("10:15", "10:45")));
    }

    [Fact]
    public void ASlotThatSwallowsAnother_Conflicts()
    {
        Assert.True(Bookings.Conflicts([At("10:00", "11:00")], At("09:00", "17:00")));
    }

    [Fact]
    public void TheSameSlotTwice_Conflicts()
    {
        Assert.True(Bookings.Conflicts([At("10:00", "11:00")], At("10:00", "11:00")));
    }

    [Fact]
    public void AZeroLengthSlot_DoesNotConflictAtAnEdge()
    {
        Assert.False(Bookings.Conflicts([At("10:00", "11:00")], At("11:00", "11:00")));
        Assert.False(Bookings.Conflicts([At("10:00", "11:00")], At("10:00", "10:00")));
    }

    [Fact]
    public void AZeroLengthSlot_ConflictsStrictlyInside()
    {
        Assert.True(Bookings.Conflicts([At("10:00", "11:00")], At("10:30", "10:30")));
    }

    [Fact]
    public void AConflictIsFoundAmongManySlots()
    {
        Slot[] diary = [At("09:00", "09:30"), At("10:00", "11:00"), At("14:00", "15:00")];

        Assert.True(Bookings.Conflicts(diary, At("10:45", "11:15")));
        Assert.False(Bookings.Conflicts(diary, At("11:00", "14:00")));
    }

    [Fact]
    public void ACandidateThatEndsBeforeItStarts_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            "candidate",
            () => Bookings.Conflicts([At("10:00", "11:00")], At("12:00", "11:00")));
    }

    [Fact]
    public void ABackwardsSlotInTheDiary_IsRejectedEvenWhenAnEarlierSlotAlreadyConflicts()
    {
        // The conflict is in the first slot and the fault is in the second. Answering the question
        // before reading the whole diary is how a bad row survives in it.
        Slot[] diary = [At("10:00", "11:00"), At("16:00", "15:00")];

        Assert.Throws<ArgumentException>("existing", () => Bookings.Conflicts(diary, At("10:30", "10:45")));
    }

    [Fact]
    public void ADiaryThatIsNotThere_IsRejected()
    {
        Assert.Throws<ArgumentNullException>("existing", () => Bookings.Conflicts(null!, At("10:00", "11:00")));
    }

    private static Slot At(string start, string end) => new(
        Day + TimeSpan.Parse(start, CultureInfo.InvariantCulture),
        Day + TimeSpan.Parse(end, CultureInfo.InvariantCulture));
}
