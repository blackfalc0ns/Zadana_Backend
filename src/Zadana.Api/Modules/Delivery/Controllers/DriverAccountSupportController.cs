using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Api.Security;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/drivers/account-support")]
[Tags("Driver App API")]
public class DriverAccountSupportController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IOrderSupportCaseWorkflowService _workflowService;

    public DriverAccountSupportController(
        IApplicationDbContext dbContext,
        IOrderSupportCaseWorkflowService workflowService)
    {
        _dbContext = dbContext;
        _workflowService = workflowService;
    }

    [HttpPost("appeals")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    public async Task<ActionResult<DriverAccountAppealAcceptedResponse>> CreatePublicAppeal(
        [FromBody] DriverAccountAppealRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Message))
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Identifier and message are required.");
        }

        var identifier = request.Identifier.Trim();
        var driver = await _dbContext.Drivers
            .Include(item => item.User)
            .FirstOrDefaultAsync(item =>
                item.User.Role == UserRole.Driver &&
                (item.User.Email == identifier.ToLowerInvariant() || item.User.PhoneNumber == identifier),
                cancellationToken);

        if (driver is not null)
        {
            await _workflowService.CreateDriverAccountAppealAsync(
                driver.Id,
                driver.UserId,
                request.ReasonCode,
                request.Message,
                request.Attachments?.Select(item => new OrderSupportCaseAttachmentInput(item.FileName, item.FileUrl)).ToList(),
                cancellationToken);
        }

        return Accepted(new DriverAccountAppealAcceptedResponse(
            "If the driver account exists, the support request has been received.",
            "إذا كان حساب المندوب موجودًا، فقد استلمنا طلب الدعم.",
            "If the driver account exists, the support request has been received."));
    }
}
