using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.DTOs;

namespace Zadana.Application.Modules.Orders.Commands.ClearCart;

public record ClearCartCommand(Guid UserId) : IRequest<CartClearResponseDto>;

public class ClearCartCommandValidator : AbstractValidator<ClearCartCommand>
{
    public ClearCartCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
    }
}

public class ClearCartCommandHandler : IRequestHandler<ClearCartCommand, CartClearResponseDto>
{
    private readonly IApplicationDbContext _context;

    public ClearCartCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CartClearResponseDto> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);

        if (cart is not null)
        {
            _context.Carts.Remove(cart);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new CartClearResponseDto("cart cleared successfully");
    }
}
