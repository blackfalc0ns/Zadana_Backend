namespace Zadana.Application.Modules.Geography;

public static class GeographyCoverageConstants
{
    public const string AllRegionsToken = "all";

    public const string UnmappedCityCode = "__UNMAPPED__";

    public static class GapFlags
    {
        public const string NoVendor = "NoVendor";
        public const string NoDriver = "NoDriver";
        public const string NoSupply = "NoSupply";
        public const string DemandWithoutBoth = "DemandWithoutBoth";
        public const string NoActivity = "NoActivity";
        public const string SupplyWithoutDemand = "SupplyWithoutDemand";
    }
}
