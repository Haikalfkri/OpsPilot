using OpsPilot.Application.Common.Models;
using Xunit;

namespace OpsPilot.Application.Tests.Common;

public class PagedResultTests
{
    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(45, 20, 3)]
    public void TotalPages_RoundsUp(int totalCount, int pageSize, int expectedPages)
    {
        var page = new PagedResult<int>(Array.Empty<int>(), Page: 1, PageSize: pageSize, TotalCount: totalCount);

        Assert.Equal(expectedPages, page.TotalPges);
    }

    [Fact]
    public void TotalPages_WithZeroPageSize_IsZero_InsteadOfDividingByZero()
    {
        var page = new PagedResult<int>(Array.Empty<int>(), 1, 0, 10);

        Assert.Equal(0, page.TotalPges);
    }

    [Fact]
    public void Map_ConvertsItems_AndKeepsPagingNumbers()
    {
        var page = new PagedResult<int>(new[] { 1, 2, 3 }, Page: 2, PageSize: 3, TotalCount: 9);

        var mapped = page.Map(x => x * 10);

        Assert.Equal(new[] { 10, 20, 30 }, mapped.Items);
        Assert.Equal(2, mapped.Page);
        Assert.Equal(3, mapped.PageSize);
        Assert.Equal(9, mapped.TotalCount);
    }
}