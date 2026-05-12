using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Services;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.ChangeTemporaryPassword;

public record ChangeTemporaryPasswordCommand(string CurrentPassword, string NewPassword) : IRequest;

public sealed class ChangeTemporaryPasswordCommandHandler : IRequestHandler<ChangeTemporaryPasswordCommand>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IAccessAuditService _auditService;

    public ChangeTemporaryPasswordCommandHandler(
        ICurrentUserService currentUserService,
        IApplicationDbContext context,
        IIdentityAccountService identityAccountService,
        IAccessAuditService auditService)
    {
        _currentUserService = currentUserService;
        _context = context;
        _identityAccountService = identityAccountService;
        _auditService = auditService;
    }

    public async Task Handle(ChangeTemporaryPasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("USER_NOT_FOUND");

        if (!user.MustChangePassword)
        {
            return;
        }

        var result = await _identityAccountService.ChangePasswordAsync(
            user.Id,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new BadRequestException(
                "PASSWORD_CHANGE_FAILED",
                string.Join(", ", result.Errors ?? ["Unable to change password."]));
        }

        _auditService.Add(user.Id, "temporary-password-changed", "Temporary password changed by user.");
        await _context.SaveChangesAsync(cancellationToken);
    }
}
