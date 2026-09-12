namespace Billing;

/// <summary>
/// The plausible wrong answer: the split moves to whole cents, so the shares now sum to the
/// invoice — and the whole remainder is handed to the first payee in one lump, which is a different
/// rule from the one the tests pin.
/// </summary>
public static class InvoiceSplitter
{
    private const int CentsPerUnit = 100;

    public static IReadOnlyList<decimal> Split(decimal amount, int payees)
    {
        RequireAtLeastOnePayee(payees);

        var cents = WholeCentsOf(amount);
        var share = cents / payees;
        var spare = cents % payees;

        var shares = new decimal[payees];

        for (var payee = 0; payee < payees; payee++)
        {
            shares[payee] = (decimal)share / CentsPerUnit;
        }

        shares[0] += (decimal)spare / CentsPerUnit;

        return shares;
    }

    private static void RequireAtLeastOnePayee(int payees)
    {
        if (payees <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payees), payees, "An invoice needs at least one payee.");
        }
    }

    private static long WholeCentsOf(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "An invoice cannot be negative.");
        }

        var cents = amount * CentsPerUnit;

        if (cents != decimal.Truncate(cents))
        {
            throw new ArgumentException("An invoice is a whole number of cents.", nameof(amount));
        }

        return (long)cents;
    }
}
