using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.RegisterCustomer;

public class RegisterCustomerCommandHandler : IRequestHandler<RegisterCustomerCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IOtpService _otpService;
    private readonly IApplicationDbContext _context;

    public RegisterCustomerCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IOtpService otpService,
        IApplicationDbContext context)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _otpService = otpService;
        _context = context;
    }

    public async Task<AuthResponseDto> Handle(RegisterCustomerCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await _userRepository.GetByIdentifierAsync(request.Email, cancellationToken)
                        ?? await _userRepository.GetByIdentifierAsync(request.Phone, cancellationToken);

        if (existingUser != null)
        {
            throw new BusinessRuleException("USER_ALREADY_EXISTS", "البريد الإلكتروني أو رقم الهاتف مسجل بالفعل. | Email or phone is already registered.");
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User(
            request.FullName,
            request.Email,
            request.Phone,
            passwordHash,
            UserRole.Customer,
            request.ProfilePhotoUrl);

        // Generate and log OTP
        var otpCode = user.GenerateOtp();
        await _otpService.SendOtpSmsAsync(user.Phone, otpCode, cancellationToken);
        
        _userRepository.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // --- Address Integration ---
        AddressLabel? parsedLabel = null;
        if (!string.IsNullOrWhiteSpace(request.Label) && Enum.TryParse<AddressLabel>(request.Label, true, out var l))
        {
            parsedLabel = l;
        }

        var address = new CustomerAddress(
            userId: user.Id,
            contactName: user.FullName,
            contactPhone: user.Phone,
            addressLine: request.AddressLine,
            label: parsedLabel,
            buildingNo: request.BuildingNo,
            floorNo: request.FloorNo,
            apartmentNo: request.ApartmentNo,
            city: request.City,
            area: request.Area,
            latitude: request.Latitude,
            longitude: request.Longitude
        );
        address.SetAsDefault();

        _context.CustomerAddresses.Add(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var tokens = await _jwtTokenService.GenerateTokenPairAsync(user, cancellationToken);

        var refreshTokenEntity = new Domain.Modules.Identity.Entities.RefreshToken(user.Id, tokens.RefreshToken, DateTime.UtcNow.AddDays(7));
        // Save refresh token if needed via repository

        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.Phone, user.Role.ToString());
        return new AuthResponseDto(tokens, userDto);
    }
}
