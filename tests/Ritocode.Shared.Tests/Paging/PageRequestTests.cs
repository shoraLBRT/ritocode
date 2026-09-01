using Ritocode.Shared.Errors;
using Ritocode.Shared.Paging;

namespace Ritocode.Shared.Tests.Paging;

public sealed class PageRequestTests
{
    [Fact]
    public void Create_WithNoValues_UsesDefaults()
    {
        var result = PageRequest.Create(page: null, pageSize: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(PageRequest.FirstPage, result.Value.Page);
        Assert.Equal(PageRequest.DefaultPageSize, result.Value.PageSize);
        Assert.Equal(0, result.Value.Offset);
    }

    [Fact]
    public void Offset_SkipsWholePages()
    {
        var result = PageRequest.Create(page: 4, pageSize: 25);

        Assert.True(result.IsSuccess);
        Assert.Equal(75, result.Value.Offset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithPageBelowFirst_Fails(int page)
    {
        var result = PageRequest.Create(page, pageSize: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("page", result.Error.Fields!.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(PageRequest.MaxPageSize + 1)]
    public void Create_WithPageSizeOutOfRange_FailsInsteadOfClamping(int pageSize)
    {
        var result = PageRequest.Create(page: null, pageSize);

        Assert.False(result.IsSuccess);
        Assert.Contains("pageSize", result.Error.Fields!.Keys);
    }

    [Fact]
    public void Create_WithBothInvalid_ReportsBothFields()
    {
        var result = PageRequest.Create(page: 0, pageSize: 5_000);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Error.Fields!.Count);
    }

    [Fact]
    public void Create_AtMaxPageSize_Succeeds()
    {
        var result = PageRequest.Create(page: 1, PageRequest.MaxPageSize);

        Assert.True(result.IsSuccess);
    }
}
