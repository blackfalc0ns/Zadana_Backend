using Microsoft.AspNetCore.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.RegisterVendor;

public class RegisterVendorCommandHandler : IRequestHandler<RegisterVendorCommand, AuthResponseDto>
{
    private readonly UserManager<User> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IApplicationDbContext _dbContext;

    public RegisterVendorCommandHandler(
        UserManager<User> userManager,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        IApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _dbContext = dbContext;
    }

    public async Task<AuthResponseDto> Handle(RegisterVendorCommand request, CancellationToken cancellationToken)
    {
        // 1. Check uniqueness
        var existingUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == request.Email || u.PhoneNumber == request.Phone, cancellationToken);

        if (existingUser != null)
            throw new BusinessRuleException("USER_ALREADY_EXISTS", string.Empty);

        // 2. Create User
        var user = new User(
            request.FullName,
            request.Email,
            request.Phone,
            UserRole.Vendor);

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessRuleException("CREATION_FAILED", $"فشل إنشاء حساب التاجر. | Failed to create vendor account. ({errors})");
        }

        // 3. Create Vendor (PendingReview)
        var vendor = new Vendor(
            user.Id,
            request.BusinessNameAr,
            request.BusinessNameEn,
            request.BusinessType,
            request.CommercialRegistrationNumber,
            request.ContactEmail,
            request.ContactPhone,
            request.TaxId,
            request.LogoUrl,
            request.CommercialRegisterDocumentUrl);

        _dbContext.Vendors.Add(vendor);
        await _unitOfWork.SaveChangesAsync(cancellationToken); // Save to get Vendor.Id

        // 4. Create initial VendorBranch
        var branch = new VendorBranch(
            vendor.Id,
            request.BranchName,
            request.BranchAddressLine,
            request.BranchLatitude,
            request.BranchLongitude,
            request.BranchContactPhone,
            request.BranchDeliveryRadiusKm);

        _dbContext.VendorBranches.Add(branch);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Generate tokens
        var tokens = await _jwtTokenService.GenerateTokenPairAsync(user, cancellationToken);
        var refreshTokenEntity = new RefreshToken(user.Id, tokens.RefreshToken, DateTime.UtcNow.AddDays(7));
        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString());
        return new AuthResponseDto(tokens, userDto);
    }
}
