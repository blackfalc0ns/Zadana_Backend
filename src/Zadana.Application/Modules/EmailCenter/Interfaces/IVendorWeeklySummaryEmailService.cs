namespace Zadana.Application.Modules.EmailCenter.Interfaces;

public interface IVendorWeeklySummaryEmailService
{
    Task<int> DispatchWeeklySummariesAsync(
        DateTime weekStartUtc,
        DateTime weekEndUtc,
        CancellationToken cancellationToken = default);
}
