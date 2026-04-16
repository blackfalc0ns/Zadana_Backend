using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.CreateOrderComplaint;

public record CreateOrderComplaintCommand(
    Guid OrderId,
    Guid UserId,
    string Message,
    IReadOnlyList<CreateOrderComplaintAttachmentItem>? Attachments) : IRequest<OrderComplaintDto>;

public record CreateOrderComplaintAttachmentItem(string FileName, string FileUrl);

public class CreateOrderComplaintCommandValidator : AbstractValidator<CreateOrderComplaintCommand>
{
    public CreateOrderComplaintCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        var messageRequired = localizer["RequiredField"].Value.Replace("{PropertyName}", "message");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage(messageRequired)
            .MaximumLength(2000);

        RuleForEach(x => x.Attachments).ChildRules(attachment =>
        {
            attachment.RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
            attachment.RuleFor(x => x.FileUrl).NotEmpty().MaximumLength(2000);
        });
    }
}

public class CreateOrderComplaintCommandHandler : IRequestHandler<CreateOrderComplaintCommand, OrderComplaintDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderComplaintCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderComplaintDto> Handle(CreateOrderComplaintCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == request.OrderId && x.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        var existingComplaint = await _context.OrderComplaints
            .AnyAsync(x => x.OrderId == order.Id, cancellationToken);

        if (existingComplaint)
        {
            throw new BusinessRuleException("ORDER_COMPLAINT_ALREADY_EXISTS", "A complaint has already been submitted for this order.");
        }

        var complaint = new OrderComplaint(order.Id, request.Message);

        foreach (var attachment in request.Attachments ?? [])
        {
            complaint.AddAttachment(attachment.FileName, attachment.FileUrl);
        }

        _context.OrderComplaints.Add(complaint);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new OrderComplaintDto(
            complaint.Id,
            "submitted",
            complaint.Message,
            complaint.Attachments
                .Select(x => new OrderComplaintAttachmentDto(x.FileName, x.FileUrl))
                .ToList(),
            complaint.CreatedAtUtc);
    }
}
