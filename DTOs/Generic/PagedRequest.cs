namespace ECommerce.DTOs.Generic;

public class PagedRequest
{
    public const int MaxPageSize = 100;

    private int _pageNumber = 1;
    private int _pageSize = 20;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";

    public bool Descending => string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
}
