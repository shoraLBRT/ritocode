using Xunit;

namespace Billing.Tests;

public sealed class InvoiceSplitterTests
{
    [Fact]
    public void OnePayee_OwesTheWholeInvoice()
    {
        Assert.Equal(new[] { 42.00m }, InvoiceSplitter.Split(42.00m, 1));
    }

    [Fact]
    public void AnInvoiceThatDividesEvenly_SplitsEvenly()
    {
        Assert.Equal(new[] { 25.00m, 25.00m, 25.00m, 25.00m }, InvoiceSplitter.Split(100.00m, 4));
    }

    [Fact]
    public void NothingOwed_IsSplitIntoNothings()
    {
        Assert.Equal(new[] { 0m, 0m, 0m }, InvoiceSplitter.Split(0m, 3));
    }

    [Fact]
    public void TheShares_AlwaysSumToTheInvoice()
    {
        foreach (var payees in new[] { 1, 2, 3, 6, 7, 11, 13 })
        {
            Assert.Equal(10.00m, InvoiceSplitter.Split(10.00m, payees).Sum());
        }
    }

    [Fact]
    public void AnInvoiceThatDoesNotDivide_GivesTheSpareCentsToTheEarliestPayees()
    {
        // 1000 cents over seven payees is 142 each with six left over.
        Assert.Equal(
            new[] { 1.43m, 1.43m, 1.43m, 1.43m, 1.43m, 1.43m, 1.42m },
            InvoiceSplitter.Split(10.00m, 7));
    }

    [Fact]
    public void NoTwoShares_DifferByMoreThanACent()
    {
        var shares = InvoiceSplitter.Split(10.00m, 7);

        Assert.Equal(0.01m, shares.Max() - shares.Min());
    }

    [Fact]
    public void AnInvoiceOfFractionsOfACent_IsRejected()
    {
        Assert.Throws<ArgumentException>("amount", () => InvoiceSplitter.Split(10.001m, 2));
    }

    [Fact]
    public void ANegativeInvoice_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>("amount", () => InvoiceSplitter.Split(-1.00m, 2));
    }

    [Fact]
    public void ASplitBetweenNobody_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>("payees", () => InvoiceSplitter.Split(10.00m, 0));
    }
}
