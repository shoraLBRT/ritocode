namespace Orders;

/// <summary>
/// A refactoring that reads better and computes something else: the coupon table was
/// mis-transcribed on the way into it, so SAVE10 takes 20% off. It compiles, it is tidy, and the
/// tests catch it. This is the answer the verdict has to reject.
/// </summary>
public static class OrderTotal
{
    private const decimal FreeShippingThreshold = 100m;
    private const decimal ShippingCharge = 9.99m;

    private static readonly Dictionary<string, decimal> CouponDiscounts = new(StringComparer.Ordinal)
    {
        ["SAVE10"] = 0.20m,
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

        var subtotal = lines.Sum(line => line.Quantity * line.UnitPrice);
        var goods = subtotal - Discount(subtotal, couponCode);
        var total = goods + Tax(goods, country) + Shipping(goods);

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal Discount(decimal subtotal, string? couponCode) =>
        couponCode is not null && CouponDiscounts.TryGetValue(couponCode, out var rate)
            ? subtotal * rate
            : 0m;

    private static decimal Tax(decimal goods, string country) =>
        TaxRates.TryGetValue(country, out var rate) ? goods * rate : 0m;

    private static decimal Shipping(decimal goods) =>
        goods < FreeShippingThreshold ? ShippingCharge : 0m;
}
