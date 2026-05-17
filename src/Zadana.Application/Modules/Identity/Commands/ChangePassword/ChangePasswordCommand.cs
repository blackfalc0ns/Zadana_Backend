using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Services;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.ChangePassword;

public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8);
    }
}

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IAccessAuditService _auditService;

    public ChangePasswordCommandHandler(
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

    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("USER_NOT_FOUND");

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

        user.CompletePasswordChange();
        _auditService.Add(user.Id, "password-changed", "Password changed by user.");
        await _context.SaveChangesAsync(cancellationToken);
    }
}
