using MediatR;
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
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IApplicationDbContext _dbContext;

    public RegisterVendorCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IApplicationDbContext dbContext)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _dbContext = dbContext;
    }

    public async Task<AuthResponseDto> Handle(RegisterVendorCommand request, CancellationToken cancellationToken)
    {
        // 1. Check uniqueness
        var existingUser = await _userRepository.GetByIdentifierAsync(request.Email, cancellationToken)
                        ?? await _userRepository.GetByIdentifierAsync(request.Phone, cancellationToken);

        if (existingUser != null)
            throw new BusinessRuleException("USER_ALREADY_EXISTS", string.Empty);

        // 2. Create User
        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = new User(
            request.FullName,
            request.Email,
            request.Phone,
            passwordHash,
            UserRole.Vendor);

        _userRepository.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken); // Save to get User.Id

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

        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.Phone, user.Role.ToString());
        return new AuthResponseDto(tokens, userDto);
    }
}
