using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.SubmitRequest;

public class SubmitProductRequestCommandHandler : IRequestHandler<SubmitProductRequestCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SubmitProductRequestCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(SubmitProductRequestCommand request, CancellationToken cancellationToken)
    {
        var vendorId = _currentUserService.UserId 
            ?? throw new UnauthorizedAccessException("يجب أن تكون مسجلاً كتاجر | You must be logged in as a vendor.");
            
        if (_currentUserService.Role != "Vendor")
            throw new UnauthorizedAccessException("فقط التجار يمكنهم طلب منتجات جديدة | Only vendors can request new products.");

        // Validate category exists
        var categoryExists = await _context.Categories.FindAsync([request.SuggestedCategoryId], cancellationToken);
        if (categoryExists == null)
            throw new NotFoundException(nameof(Category), request.SuggestedCategoryId);

        var productRequest = new ProductRequest(
            vendorId: vendorId,
            suggestedNameAr: request.SuggestedNameAr,
            suggestedNameEn: request.SuggestedNameEn,
            suggestedCategoryId: request.SuggestedCategoryId,
            suggestedDescriptionAr: request.SuggestedDescriptionAr,
            suggestedDescriptionEn: request.SuggestedDescriptionEn,
            imageUrl: request.ImageUrl
        );

        _context.ProductRequests.Add(productRequest);
        await _context.SaveChangesAsync(cancellationToken);

        return productRequest.Id;
    }
}
