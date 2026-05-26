using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Application.Modules.EmailCenter.Interfaces;

public interface IEmailCenterService
{
    Task<EmailCenterOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<EmailWorkflowRuleDto> UpdateRuleAsync(
        string ruleId,
        EmailWorkflowRuleDto draft,
        CancellationToken cancellationToken = default);

    Task<EmailResolvedRecipientsDto> ResolveRecipientsAsync(
        string ruleId,
        EmailWorkflowRuleDto draft,
        CancellationToken cancellationToken = default);

    Task<EmailTestSendResultDto> TestSendAsync(
        string ruleId,
        EmailWorkflowRuleDto draft,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailDispatchLogDto>> GetDispatchesAsync(
        string? ruleId,
        string? source,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<EmailDispatchOperationResult> DispatchVendorEmailAsync(
        Vendor vendor,
        VendorCommunicationMessage message,
        CancellationToken cancellationToken = default);

    Task<EmailDispatchOperationResult> DispatchSystemEventEmailAsync(
        EmailSystemEventDispatchRequest request,
        CancellationToken cancellationToken = default);
}
