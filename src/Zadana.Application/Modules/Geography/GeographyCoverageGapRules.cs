namespace Zadana.Application.Modules.Geography;

public static class GeographyCoverageGapRules
{
    public static IReadOnlyList<string> BuildGapFlags(
        int customers,
        int activeVendors,
        int readyDrivers,
        int verifiedDrivers = 0)
    {
        if (customers > 0)
        {
            return BuildDemandGapFlags(customers, activeVendors, readyDrivers);
        }

        if (activeVendors > 0 || readyDrivers > 0 || verifiedDrivers > 0)
        {
            return [GeographyCoverageConstants.GapFlags.SupplyWithoutDemand];
        }

        return [GeographyCoverageConstants.GapFlags.NoActivity];
    }

    public static bool HasOperationalGap(IReadOnlyList<string> gapFlags) =>
        gapFlags.Any(flag => flag != GeographyCoverageConstants.GapFlags.NoActivity);

    public static bool IsFullyCovered(IReadOnlyList<string> gapFlags) => gapFlags.Count == 0;

    public static int GapSeverityScore(IReadOnlyList<string> gapFlags)
    {
        if (gapFlags.Contains(GeographyCoverageConstants.GapFlags.DemandWithoutBoth))
        {
            return 4;
        }

        if (gapFlags.Contains(GeographyCoverageConstants.GapFlags.NoSupply))
        {
            return 3;
        }

        if (gapFlags.Contains(GeographyCoverageConstants.GapFlags.SupplyWithoutDemand))
        {
            return 2;
        }

        if (gapFlags.Contains(GeographyCoverageConstants.GapFlags.NoVendor)
            && gapFlags.Contains(GeographyCoverageConstants.GapFlags.NoDriver))
        {
            return 2;
        }

        if (gapFlags.Contains(GeographyCoverageConstants.GapFlags.NoActivity))
        {
            return 0;
        }

        return gapFlags.Count > 0 ? 1 : 0;
    }

    private static IReadOnlyList<string> BuildDemandGapFlags(
        int customers,
        int activeVendors,
        int readyDrivers)
    {
        _ = customers;

        var gaps = new List<string>();
        if (activeVendors == 0)
        {
            gaps.Add(GeographyCoverageConstants.GapFlags.NoVendor);
        }

        if (readyDrivers == 0)
        {
            gaps.Add(GeographyCoverageConstants.GapFlags.NoDriver);
        }

        if (activeVendors == 0 || readyDrivers == 0)
        {
            gaps.Add(GeographyCoverageConstants.GapFlags.NoSupply);
        }

        if (activeVendors == 0 && readyDrivers == 0)
        {
            gaps.Add(GeographyCoverageConstants.GapFlags.DemandWithoutBoth);
        }

        return gaps;
    }
}
