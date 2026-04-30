using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
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
    private readonly IOrderSupportCaseWorkflowService _workflowService;

    public CreateOrderComplaintCommandHandler(IOrderSupportCaseWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public async Task<OrderComplaintDto> Handle(CreateOrderComplaintCommand request, CancellationToken cancellationToken)
    {
        var supportCase = await _workflowService.CreateCustomerCaseAsync(
            request.OrderId,
            request.UserId,
            "complaint",
            reasonCode: null,
            request.Message,
            request.Attachments?.Select(item => new OrderSupportCaseAttachmentInput(item.FileName, item.FileUrl)).ToList(),
            cancellationToken);

        return new OrderComplaintDto(
            supportCase.Id,
            "submitted",
            supportCase.Message,
            supportCase.Attachments
                .Select(x => new OrderComplaintAttachmentDto(x.FileName, x.FileUrl))
                .ToList(),
            supportCase.CreatedAtUtc);
    }
}
