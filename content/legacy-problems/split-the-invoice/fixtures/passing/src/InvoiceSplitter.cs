namespace Billing;

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
            var owed = share + (payee < spare ? 1 : 0);
            shares[payee] = (decimal)owed / CentsPerUnit;
        }

        return shares;
    }

    private static void RequireAtLeastOnePayee(int payees)
    {
        if (payees <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payees), payees, "An invoice needs at least one payee.");
        }
    }

    /// <summary>
    /// The whole point of the exercise: the split happens in cents, so the leftover is a remainder
    /// rather than a rounding error nobody accounts for.
    /// </summary>
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
