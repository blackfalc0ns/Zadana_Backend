using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.RegisterUser;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // 1. Check if email exists
        var emailExists = _context.Users.Any(u => u.Email == request.Email);
        if (emailExists)
        {
            throw new BusinessRuleException(
                "User.EmailConflict", 
                "البريد الإلكتروني مسجل بالفعل. | Email is already in use.");
        }

        // 2. Parse Role
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            throw new BusinessRuleException(
                "User.InvalidRole", 
                "دور المستخدم غير صالح. | Invalid user role specified.");
        }

        // 3. Hash Password
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // 4. Create Entity
        var user = new User(
            request.FullName,
            request.Email,
            request.Phone,
            passwordHash,
            role);

        // 5. Add and Save
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        // 6. Return new Guid
        return user.Id;
    }
}
