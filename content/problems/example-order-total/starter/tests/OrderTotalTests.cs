using Xunit;

namespace Orders.Tests;

public sealed class OrderTotalTests
{
    [Fact]
    public void EmptyOrder_CostsNothing()
    {
        Assert.Equal(0m, OrderTotal.Calculate([], couponCode: null, country: "US"));
    }

    [Fact]
    public void SmallOrder_PaysTaxAndShipping()
    {
        var lines = new[] { new OrderLine("desk-lamp", 2, 25.00m) };

        // 50.00 goods + 7% tax + 9.99 shipping
        Assert.Equal(63.49m, OrderTotal.Calculate(lines, couponCode: null, country: "US"));
    }

    [Fact]
    public void CouponIsAppliedBeforeTax_AndCanEarnFreeShipping()
    {
        var lines = new[] { new OrderLine("desk-lamp", 3, 40.00m) };

        // 120.00 goods, 10% off = 108.00, + 19% tax, shipping free at 100.00
        Assert.Equal(128.52m, OrderTotal.Calculate(lines, "SAVE10", country: "DE"));
    }

    [Fact]
    public void UnknownCoupon_ChangesNothing()
    {
        var lines = new[] { new OrderLine("desk-lamp", 3, 40.00m) };

        Assert.Equal(
            OrderTotal.Calculate(lines, couponCode: null, country: "DE"),
            OrderTotal.Calculate(lines, "FREE-STUFF", country: "DE"));
    }

    [Fact]
    public void UntaxedCountry_PaysGoodsAndShippingOnly()
    {
        var lines = new[] { new OrderLine("desk-lamp", 1, 30.00m) };

        Assert.Equal(39.99m, OrderTotal.Calculate(lines, couponCode: null, country: "NO"));
    }

    [Fact]
    public void ShippingIsFree_OnceGoodsReachTheThreshold()
    {
        var lines = new[] { new OrderLine("desk-lamp", 4, 25.00m) };

        // 100.00 goods, no tax, shipping free exactly at the threshold
        Assert.Equal(100.00m, OrderTotal.Calculate(lines, couponCode: null, country: "NO"));
    }
}
