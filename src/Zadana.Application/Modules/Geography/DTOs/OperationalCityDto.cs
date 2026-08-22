namespace Zadana.Application.Modules.Geography.DTOs;

public sealed class OperationalCityDto
{
    public Guid Id { get; init; }
    public string RegionCode { get; init; } = null!;
    public string Code { get; init; } = null!;
    public string NameAr { get; init; } = null!;
    public string NameEn { get; init; } = null!;
    public int SortOrder { get; init; }
    public bool IsOperational { get; init; }
}
