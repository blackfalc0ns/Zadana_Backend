using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Authorization;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Domain.Modules.Identity.Constants;

namespace Zadana.Api.Modules.EmailCenter.Controllers;

[Route("api/admin/email-center")]
[Authorize(Policy = "AdminOnly")]
public class AdminEmailCenterController : ApiControllerBase
{
    private readonly IEmailCenterService _emailCenterService;

    public AdminEmailCenterController(IEmailCenterService emailCenterService)
    {
        _emailCenterService = emailCenterService;
    }

    [HttpGet("overview")]
    [RequireAccess(PermissionKeys.Admin.EmailCenterView)]
    public async Task<ActionResult<EmailCenterOverviewDto>> GetOverview(CancellationToken cancellationToken)
    {
        return Ok(await _emailCenterService.GetOverviewAsync(cancellationToken));
    }

    [HttpPut("rules/{id}")]
    [RequireAccess(PermissionKeys.Admin.EmailCenterEdit)]
    public async Task<ActionResult<EmailWorkflowRuleDto>> UpdateRule(
        string id,
        [FromBody] EmailWorkflowRuleDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _emailCenterService.UpdateRuleAsync(id, request, cancellationToken));
    }

    [HttpPost("rules/{id}/resolve-recipients")]
    [RequireAccess(PermissionKeys.Admin.EmailCenterView)]
    public async Task<ActionResult<EmailResolvedRecipientsDto>> ResolveRecipients(
        string id,
        [FromBody] EmailWorkflowRuleDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _emailCenterService.ResolveRecipientsAsync(id, request, cancellationToken));
    }

    [HttpPost("rules/{id}/test-send")]
    [RequireAccess(PermissionKeys.Admin.EmailCenterEdit)]
    public async Task<ActionResult<EmailTestSendResultDto>> TestSend(
        string id,
        [FromBody] EmailWorkflowRuleDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _emailCenterService.TestSendAsync(id, request, cancellationToken));
    }

    [HttpGet("dispatches")]
    [RequireAccess(PermissionKeys.Admin.EmailCenterView)]
    public async Task<ActionResult<IReadOnlyList<EmailDispatchLogDto>>> GetDispatches(
        [FromQuery] string? ruleId,
        [FromQuery] string? source,
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken)
    {
        return Ok(await _emailCenterService.GetDispatchesAsync(ruleId, source, status, dateFrom, dateTo, cancellationToken));
    }
}
