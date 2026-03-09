using Microsoft.AspNetCore.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Delivery.Commands.RegisterDriver;

public class RegisterDriverCommandHandler : IRequestHandler<RegisterDriverCommand, AuthResponseDto>
{
    private readonly UserManager<User> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IApplicationDbContext _dbContext;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RegisterDriverCommandHandler(
        UserManager<User> userManager,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        IApplicationDbContext dbContext,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _dbContext = dbContext;
        _localizer = localizer;
    }

    public async Task<AuthResponseDto> Handle(RegisterDriverCommand request, CancellationToken cancellationToken)
    {
        // 1. Check uniqueness
        var existingUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == request.Email || u.PhoneNumber == request.Phone, cancellationToken);

        if (existingUser != null)
        {
            throw new BusinessRuleException("USER_ALREADY_EXISTS", _localizer["USER_ALREADY_EXISTS"]);
        }

        // 2. Create User
        var user = new User(
            request.FullName,
            request.Email,
            request.Phone,
            UserRole.Driver);

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessRuleException("CREATION_FAILED", $"{_localizer["CREATION_FAILED"]}: {errors}");
        }

        // 3. Create Driver (Status = Pending)
        var driver = new Driver(
            user.Id,
            request.VehicleType,
            request.NationalId,
            request.LicenseNumber,
            request.Address,
            request.NationalIdImageUrl,
            request.LicenseImageUrl,
            request.VehicleImageUrl,
            request.PersonalPhotoUrl);

        _dbContext.Drivers.Add(driver);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Generate tokens
        var tokens = await _jwtTokenService.GenerateTokenPairAsync(user, cancellationToken);
        var refreshTokenEntity = new RefreshToken(user.Id, tokens.RefreshToken, DateTime.UtcNow.AddDays(7));
        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString());
        return new AuthResponseDto(tokens, userDto);
    }
}
