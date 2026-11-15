using ECommerce.DTOs.Generic;

namespace ECommerce.Tests.Unit;

public class ApiResponseTests
{
    [Fact]
    public void Success_uses_code_00_and_status_200()
    {
        var r = ApiResponse.Success("ok", new { a = 1 });
        Assert.Equal(ResponseCodes.Success, r.ResponseCode);
        Assert.Equal(200, r.HttpStatusCode);
    }

    [Theory]
    [InlineData(404, ResponseCodes.NotFound)]
    [InlineData(403, ResponseCodes.Forbidden)]
    [InlineData(409, ResponseCodes.Conflict)]
    public void Shortcuts_map_status_and_code(int status, string code)
    {
        var r = status switch
        {
            404 => ApiResponse.NotFound("x"),
            403 => ApiResponse.Forbidden("x"),
            _ => ApiResponse.Conflict("x")
        };
        Assert.Equal(status, r.HttpStatusCode);
        Assert.Equal(code, r.ResponseCode);
    }

    [Fact]
    public void Paged_carries_items_and_pagination()
    {
        var page = new PagedResult<int> { Items = [1, 2], Pagination = new PaginationMeta(1, 2, 7) };
        var r = ApiResponse.Paged("ok", page);
        Assert.Equal(page.Items, r.Data);
        Assert.Equal(4, r.Pagination!.TotalPages);
    }
}
