namespace Orders;

/// <summary>
/// The refactoring the task asks for: one named rule at a time, same arithmetic.
/// </summary>
public static class OrderTotal
{
    private const decimal FreeShippingThreshold = 100m;
    private const decimal ShippingCharge = 9.99m;

    private static readonly Dictionary<string, decimal> CouponDiscounts = new(StringComparer.Ordinal)
    {
        ["SAVE10"] = 0.10m,
        ["SAVE5"] = 0.05m,
    };

    private static readonly Dictionary<string, decimal> TaxRates = new(StringComparer.Ordinal)
    {
        ["US"] = 0.07m,
        ["DE"] = 0.19m,
        ["GB"] = 0.20m,
    };

    public static decimal Calculate(IReadOnlyList<OrderLine> lines, string? couponCode, string country)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            return 0m;
        }

        var goods = Subtotal(lines) - Discount(Subtotal(lines), couponCode);
        var total = goods + Tax(goods, country) + Shipping(goods);

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal Subtotal(IReadOnlyList<OrderLine> lines) =>
        lines.Sum(line => line.Quantity * line.UnitPrice);

    private static decimal Discount(decimal subtotal, string? couponCode) =>
        couponCode is not null && CouponDiscounts.TryGetValue(couponCode, out var rate)
            ? subtotal * rate
            : 0m;

    private static decimal Tax(decimal goods, string country) =>
        TaxRates.TryGetValue(country, out var rate) ? goods * rate : 0m;

    private static decimal Shipping(decimal goods) =>
        goods < FreeShippingThreshold ? ShippingCharge : 0m;
}
