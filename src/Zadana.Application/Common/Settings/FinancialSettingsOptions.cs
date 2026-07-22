namespace Zadana.Application.Common.Settings;

public class FinancialSettingsOptions
{
    public const string SectionName = "FinancialSettings";

    /// <summary>
    /// Platform commission percentage on driver delivery fee (e.g. 15.0 means 15%).
    /// </summary>
    public decimal DriverCommissionRatePercent { get; set; } = 15.0m;

    /// <summary>
    /// Day of week for weekly settlement runs (e.g. "Sunday").
    /// </summary>
    public string WeeklySettlementDayOfWeek { get; set; } = "Sunday";

    /// <summary>
    /// Days of month for biweekly settlement runs (e.g. [1, 16]).
    /// </summary>
    public int[] BiweeklySettlementDaysOfMonth { get; set; } = [1, 16];

    /// <summary>
    /// Day of month for monthly settlement. 0 means last day of month.
    /// </summary>
    public int MonthlySettlementDayOfMonth { get; set; } = 0;

    /// <summary>
    /// Fixed OwnerId for the platform wallet singleton.
    /// </summary>
    public Guid PlatformWalletOwnerId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public decimal DriverMinimumWithdrawalAmount { get; set; } = 10m;

    public decimal DriverMaximumWithdrawalAmount { get; set; } = 50_000m;

    public int DriverMaximumWithdrawalRequestsPerDay { get; set; } = 3;

    public decimal DriverCodBlockThresholdAmount { get; set; } = 500m;

    public decimal GatewayFeeRatePercent { get; set; } = 2.75m;

    public decimal GatewayFeeFixedAmount { get; set; } = 1m;
}
