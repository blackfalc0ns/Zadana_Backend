using Microsoft.AspNetCore.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.RegisterCustomer;

public class RegisterCustomerCommandHandler : IRequestHandler<RegisterCustomerCommand, AuthResponseDto>
{
    private readonly UserManager<User> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IOtpService _otpService;
    private readonly IApplicationDbContext _context;

    public RegisterCustomerCommandHandler(
        UserManager<User> userManager,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        IOtpService otpService,
        IApplicationDbContext context)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _otpService = otpService;
        _context = context;
    }

    public async Task<AuthResponseDto> Handle(RegisterCustomerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BusinessRuleException("EMAIL_REQUIRED", "البريد الإلكتروني مطلوب. | Email is required.");
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser == null)
        {
            existingUser = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.Phone, cancellationToken);
        }

        if (existingUser != null)
        {
            throw new BusinessRuleException("USER_ALREADY_EXISTS", "البريد الإلكتروني أو رقم الهاتف مسجل بالفعل. | Email or phone is already registered.");
        }

        var user = new User(
            request.FullName,
            request.Email,
            request.Phone,
            UserRole.Customer,
            request.ProfilePhotoUrl);

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessRuleException("CREATION_FAILED", $"فشل إنشاء حساب المستخدم. | Failed to create user account. ({errors})");
        }

        // Generate and log OTP
        var otpCode = user.GenerateOtp();
        await _otpService.SendOtpEmailAsync(user.Email!, otpCode, cancellationToken);
        
        // No need to Add(user) or SaveChanges for the user as CreateAsync handles it.

        // --- Address Integration ---
        AddressLabel? parsedLabel = null;
        if (!string.IsNullOrWhiteSpace(request.Label) && Enum.TryParse<AddressLabel>(request.Label, true, out var l))
        {
            parsedLabel = l;
        }

        var address = new CustomerAddress(
            userId: user.Id,
            contactName: user.FullName,
            contactPhone: user.PhoneNumber,
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

        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString());
        return new AuthResponseDto(null, userDto, false, "يرجى التحقق من بريدك الإلكتروني لإكمال عملية التسجيل. | Please verify your email to complete registration.");
    }
}
