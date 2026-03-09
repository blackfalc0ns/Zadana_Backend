using Microsoft.AspNetCore.Identity;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.RegisterUser;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Guid>
{
    private readonly UserManager<User> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RegisterUserCommandHandler(UserManager<User> userManager, IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _localizer = localizer;
    }

    public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // 1. Check if email exists
        var emailExists = await _userManager.FindByEmailAsync(request.Email) != null;
        if (emailExists)
        {
            throw new BusinessRuleException(
                "User.EmailConflict", 
                _localizer["EMAIL_ALREADY_IN_USE"]);
        }

        // 2. Parse Role
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            throw new BusinessRuleException(
                "User.InvalidRole", 
                _localizer["INVALID_USER_ROLE"]);
        }

        // 3. Create Entity
        var user = new User(
            request.FullName,
            request.Email,
            request.Phone,
            role);

        // 4. Save using UserManager
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessRuleException("CREATION_FAILED", $"{_localizer["CREATION_FAILED"]}: {errors}");
        }

        // 5. Return new Guid
        return user.Id;
    }
}
