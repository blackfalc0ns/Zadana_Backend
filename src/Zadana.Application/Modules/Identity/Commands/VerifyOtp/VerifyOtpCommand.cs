using MediatR;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public record VerifyOtpCommand(
    string Identifier,
    string OtpCode,
    string? RegistrationToken = null,
    UserRole? ExpectedRole = null) : IRequest<AuthResponseDto>;
