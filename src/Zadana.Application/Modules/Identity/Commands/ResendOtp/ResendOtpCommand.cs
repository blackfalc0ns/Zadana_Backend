using MediatR;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Commands.ResendOtp;

public record ResendOtpCommand(string Identifier) : IRequest<AuthResponseDto>;
