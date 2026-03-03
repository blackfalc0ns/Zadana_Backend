using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.RegisterDriver;

public class RegisterDriverCommandHandler : IRequestHandler<RegisterDriverCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IApplicationDbContext _dbContext;

    public RegisterDriverCommandHandler(
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

    public async Task<AuthResponseDto> Handle(RegisterDriverCommand request, CancellationToken cancellationToken)
    {
        // 1. Check uniqueness
        var existingUser = await _userRepository.GetByIdentifierAsync(request.Email, cancellationToken)
                        ?? await _userRepository.GetByIdentifierAsync(request.Phone, cancellationToken);

        if (existingUser != null)
        {
            throw new BusinessRuleException("USER_ALREADY_EXISTS", "البريد الإلكتروني أو رقم الهاتف مسجل بالفعل. | Email or phone is already registered.");
        }

        // 2. Create User
        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = new User(
            request.FullName,
            request.Email,
            request.Phone,
            passwordHash,
            UserRole.Driver);

        _userRepository.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 3. Create Driver (Status = Pending → جاري مراجعة بياناتك)
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

        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.Phone, user.Role.ToString());
        return new AuthResponseDto(tokens, userDto);
    }
}
