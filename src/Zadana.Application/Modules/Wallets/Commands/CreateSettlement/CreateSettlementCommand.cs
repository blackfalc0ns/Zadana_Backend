using FluentValidation;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Domain.Modules.Wallets.Entities;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Application.Modules.Wallets.Commands.CreateSettlement;

public record CreateSettlementCommand(
    Guid? VendorId,
    Guid? DriverId,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal NetAmount) : MediatR.IRequest<Guid>;

public class CreateSettlementCommandValidator : AbstractValidator<CreateSettlementCommand>
{
    public CreateSettlementCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x)
            .Must(x => x.VendorId.HasValue || x.DriverId.HasValue)
            .WithMessage(x => localizer["EitherVendorOrDriverRequired"]);

        RuleFor(x => x.GrossAmount)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);

        RuleFor(x => x.CommissionAmount)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);

        RuleFor(x => x.NetAmount)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);

        RuleFor(x => x)
            .Must(x => Math.Abs((x.GrossAmount - x.CommissionAmount) - x.NetAmount) <= 0.01m)
            .WithMessage(x => localizer["InvalidAmount"]);
    }
}

public class CreateSettlementCommandHandler : IRequestHandler<CreateSettlementCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateSettlementCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateSettlementCommand request, CancellationToken cancellationToken)
    {
        var settlement = new Settlement(request.VendorId, request.DriverId);
        settlement.UpdateTotals(request.GrossAmount, request.CommissionAmount);

        _context.Settlements.Add(settlement);
        await _context.SaveChangesAsync(cancellationToken);

        if (request.VendorId.HasValue)
        {
            var primaryBankAccount = await _context.VendorBankAccounts
                .AsNoTracking()
                .Where(item => item.VendorId == request.VendorId.Value)
                .OrderByDescending(item => item.IsPrimary)
                .ThenByDescending(item => item.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (primaryBankAccount is not null)
            {
                var payout = new Payout(settlement.Id, settlement.NetAmount, primaryBankAccount.Id);
                _context.Payouts.Add(payout);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return settlement.Id;
    }
}
