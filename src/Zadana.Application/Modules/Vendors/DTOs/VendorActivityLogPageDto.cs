namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorActivityLogPageDto(
    IReadOnlyList<VendorActivityLogEntryDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
