namespace Orders;

public static class OrderTotal
{
    public static decimal Calculate(IReadOnlyList<OrderLine> lines, string? couponCode, string country)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            return 0m;
        }

        decimal total = 0m;

        for (var i = 0; i < lines.Count; i++)
        {
            total += lines[i].Quantity * lines[i].UnitPrice;
        }

        if (couponCode == "SAVE10")
        {
            total -= total * 0.10m;
        }
        else if (couponCode == "SAVE5")
        {
            total -= total * 0.05m;
        }

        var goodsTotal = total;

        if (country == "US")
        {
            total += goodsTotal * 0.07m;
        }
        else if (country == "DE")
        {
            total += goodsTotal * 0.19m;
        }
        else if (country == "GB")
        {
            total += goodsTotal * 0.20m;
        }

        if (goodsTotal < 100m)
        {
            total += 9.99m;
        }

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
}
