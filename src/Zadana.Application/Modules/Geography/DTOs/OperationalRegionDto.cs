namespace Zadana.Application.Modules.Geography.DTOs;

public sealed class OperationalRegionDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string NameAr { get; init; } = null!;
    public string NameEn { get; init; } = null!;
    public int SortOrder { get; init; }
    public bool IsOperational { get; init; }
    public IReadOnlyList<OperationalCityDto> Cities { get; init; } = [];
}
