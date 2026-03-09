using MediatR;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public record VerifyOtpCommand(string Identifier, string OtpCode) : IRequest<AuthResponseDto>;
