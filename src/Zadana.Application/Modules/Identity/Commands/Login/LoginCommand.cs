using MediatR;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Commands.Login;

public record LoginCommand(string Identifier, string Password, UserRole[]? ExpectedRoles = null) : IRequest<AuthResponseDto>;
