using Ritocode.Shared.Paging;

namespace Ritocode.Shared.Tests.Paging;

public sealed class PageTests
{
    private static PageRequest Request(int page, int pageSize) =>
        PageRequest.Create(page, pageSize).Value;

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(41, 20, 3)]
    public void TotalPages_RoundsUp(long totalItems, int pageSize, int expected)
    {
        var page = Page<string>.From([], Request(1, pageSize), totalItems);

        Assert.Equal(expected, page.TotalPages);
    }

    [Fact]
    public void HasNextPage_IsFalseOnLastPage()
    {
        var page = Page<string>.From(["a"], Request(3, 10), totalItems: 25);

        Assert.False(page.HasNextPage);
        Assert.True(page.HasPreviousPage);
    }

    [Fact]
    public void Empty_ReportsNoItemsButKeepsRequestedShape()
    {
        var page = Page<string>.Empty(Request(2, 50));

        Assert.Empty(page.Items);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(0, page.TotalItems);
        Assert.False(page.HasNextPage);
    }
}
