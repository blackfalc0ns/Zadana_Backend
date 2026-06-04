namespace Zadana.Application.Modules.Geography.DTOs;

public sealed class AdminGeographyCoverageDto
{
    public AdminGeographyCoverageSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<AdminGeographyCoverageCityDto> Cities { get; init; } = [];
    public IReadOnlyList<AdminGeographyCoverageRegionRollupDto> RegionRollup { get; init; } = [];
}

public sealed class AdminGeographyCoverageSummaryDto
{
    public int OfficialCityCount { get; init; }
    public int CitiesWithGaps { get; init; }
    public int CustomersWithoutVendor { get; init; }
    public int CustomersWithoutDriver { get; init; }
    public int UnmappedCustomers { get; init; }
    public IReadOnlyList<AdminGeographyCoverageTopGapDto> TopDemandGaps { get; init; } = [];
}

public sealed class AdminGeographyCoverageTopGapDto
{
    public string CityCode { get; init; } = string.Empty;
    public string CityNameAr { get; init; } = string.Empty;
    public string CityNameEn { get; init; } = string.Empty;
    public int CustomerCount { get; init; }
    public IReadOnlyList<string> GapFlags { get; init; } = [];
}

public sealed class AdminGeographyCoverageCityDto
{
    public string CityCode { get; init; } = string.Empty;
    public string RegionCode { get; init; } = string.Empty;
    public string CityNameAr { get; init; } = string.Empty;
    public string CityNameEn { get; init; } = string.Empty;
    public int CustomerCount { get; init; }
    public int ActiveVendorCount { get; init; }
    public int ReadyDriverCount { get; init; }
    public int VerifiedDriverCount { get; init; }
    public int ActiveBranchCount { get; init; }
    public IReadOnlyList<string> GapFlags { get; init; } = [];
    public AdminGeographyCoverageRoutesDto Routes { get; init; } = new();
}

public sealed class AdminGeographyCoverageRoutesDto
{
    public string Customers { get; init; } = string.Empty;
    public string Vendors { get; init; } = string.Empty;
    public string Drivers { get; init; } = string.Empty;
}

public sealed class AdminGeographyCoverageRegionRollupDto
{
    public string RegionCode { get; init; } = string.Empty;
    public string RegionNameAr { get; init; } = string.Empty;
    public string RegionNameEn { get; init; } = string.Empty;
    public int CustomerCount { get; init; }
    public int ActiveVendorCount { get; init; }
    public int ReadyDriverCount { get; init; }
    public int CitiesWithGaps { get; init; }
}
