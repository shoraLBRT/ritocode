namespace Billing;

public static class InvoiceSplitter
{
    public static IReadOnlyList<decimal> Split(decimal amount, int payees)
    {
        // Guard rails, added one support ticket at a time.
        if (payees <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payees), payees, "An invoice needs at least one payee.");
        }

        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "An invoice cannot be negative.");
        }

        if (amount * 100m != decimal.Truncate(amount * 100m))
        {
            throw new ArgumentException("An invoice is a whole number of cents.", nameof(amount));
        }

        if (amount == 0m)
        {
            var nothing = new decimal[payees];

            for (var i = 0; i < payees; i++)
            {
                nothing[i] = 0m;
            }

            return nothing;
        }

        if (payees == 1)
        {
            return new[] { Math.Round(amount, 2, MidpointRounding.AwayFromZero) };
        }

        var share = Math.Round(amount / payees, 2, MidpointRounding.ToZero);
        var shares = new decimal[payees];

        for (var i = 0; i < payees; i++)
        {
            shares[i] = share;
        }

        return shares;
    }
}
