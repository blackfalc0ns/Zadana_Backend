using MediatR;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.RegisterUser;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Guid>
{
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RegisterUserCommandHandler(IIdentityAccountService identityAccountService, IStringLocalizer<SharedResource> localizer)
    {
        _identityAccountService = identityAccountService;
        _localizer = localizer;
    }

    public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            throw new BusinessRuleException(
                "User.InvalidRole", 
                _localizer["INVALID_USER_ROLE"]);
        }

        var createResult = await _identityAccountService.CreateAsync(
            new CreateIdentityAccountRequest(
                request.FullName,
                request.Email,
                request.Phone,
                role,
                request.Password),
            cancellationToken);

        if (createResult.Status == IdentityCreateStatus.DuplicateEmailOrPhone)
        {
            throw new BusinessRuleException(
                "User.EmailConflict",
                _localizer["EMAIL_ALREADY_IN_USE"]);
        }

        if (createResult.Status != IdentityCreateStatus.Succeeded || createResult.Account == null)
        {
            var errors = string.Join(", ", createResult.Errors ?? []);
            throw new BusinessRuleException("CREATION_FAILED", $"{_localizer["CREATION_FAILED"]}: {errors}");
        }

        return createResult.Account.Id;
    }
}
