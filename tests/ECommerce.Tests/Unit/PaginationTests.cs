using ECommerce.DTOs.Generic;

namespace ECommerce.Tests.Unit;

public class PaginationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void PageNumber_is_never_below_one(int input, int expected) =>
        Assert.Equal(expected, new PagedRequest { PageNumber = input }.PageNumber);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(20, 20)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(5000, 100)]
    public void PageSize_is_clamped_to_1_and_100(int input, int expected) =>
        Assert.Equal(expected, new PagedRequest { PageSize = input }.PageSize);

    [Theory]
    [InlineData(100, 20, 5)]
    [InlineData(101, 20, 6)]
    [InlineData(0, 20, 0)]
    public void TotalPages_rounds_up(int total, int size, int pages) =>
        Assert.Equal(pages, new PaginationMeta(1, size, total).TotalPages);

    [Theory]
    [InlineData("desc", true)]
    [InlineData("DESC", true)]
    [InlineData("asc", false)]
    [InlineData("garbage", false)]
    public void Descending_is_case_insensitive(string direction, bool expected) =>
        Assert.Equal(expected, new PagedRequest { SortDirection = direction }.Descending);
}
