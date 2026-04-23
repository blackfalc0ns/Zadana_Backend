using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.ReviewDriver;

public record ReviewDriverCommand(
    Guid DriverId,
    string Action, // "approve" | "request-docs" | "reject"
    string? Note,
    Guid ReviewerUserId) : IRequest;

public class ReviewDriverCommandValidator : AbstractValidator<ReviewDriverCommand>
{
    private static readonly string[] AllowedActions = ["approve", "request-docs", "reject"];

    public ReviewDriverCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.DriverId).NotEmpty();
        RuleFor(x => x.ReviewerUserId).NotEmpty();
        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(a => AllowedActions.Contains(a.ToLowerInvariant()))
            .WithMessage("Action must be: approve, request-docs, or reject");
    }
}

public class ReviewDriverCommandHandler : IRequestHandler<ReviewDriverCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewDriverCommandHandler(IDriverRepository driverRepository, IUnitOfWork unitOfWork)
    {
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ReviewDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        switch (request.Action.ToLowerInvariant())
        {
            case "approve":
                driver.Approve(request.ReviewerUserId, request.Note);
                break;
            case "request-docs":
                driver.RequestDocuments(request.ReviewerUserId, request.Note);
                break;
            case "reject":
                driver.Reject(request.ReviewerUserId, request.Note);
                break;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
