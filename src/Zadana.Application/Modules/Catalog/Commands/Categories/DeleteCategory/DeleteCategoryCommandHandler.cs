using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.Categories.DeleteCategory;

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public DeleteCategoryCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category == null)
        {
            return;
        }

        if (category.SubCategories != null && category.SubCategories.Any())
        {
            throw new BusinessRuleException(
                "CATEGORY_HAS_SUBCATEGORIES",
                "لا يمكن حذف التصنيف لأنه يحتوي على تصنيفات فرعية.|Cannot delete this category because it has sub-categories.");
        }

        if (await _context.MasterProducts.AnyAsync(product => product.CategoryId == request.Id, cancellationToken))
        {
            throw new BusinessRuleException(
                "CATEGORY_HAS_PRODUCTS",
                "لا يمكن حذف التصنيف لأنه مرتبط بمنتجات.|Cannot delete this category because it is linked to products.");
        }

        if (await _context.Brands.AnyAsync(brand => brand.CategoryId == request.Id, cancellationToken)
            || await _context.BrandCategories.AnyAsync(link => link.CategoryId == request.Id, cancellationToken))
        {
            throw new BusinessRuleException(
                "CATEGORY_HAS_BRANDS",
                "لا يمكن حذف التصنيف لأنه مرتبط بعلامات تجارية.|Cannot delete this category because it is linked to brands.");
        }

        if (await _context.ProductTypes.AnyAsync(productType => productType.CategoryId == request.Id, cancellationToken))
        {
            throw new BusinessRuleException(
                "CATEGORY_HAS_PRODUCT_TYPES",
                "لا يمكن حذف التصنيف لأنه مرتبط بأنواع منتجات.|Cannot delete this category because it is linked to product types.");
        }

        if (await _context.ProductRequests.AnyAsync(productRequest => productRequest.SuggestedCategoryId == request.Id, cancellationToken)
            || await _context.BrandRequests.AnyAsync(brandRequest => brandRequest.CategoryId == request.Id, cancellationToken)
            || await _context.CategoryRequests.AnyAsync(categoryRequest =>
                categoryRequest.ParentCategoryId == request.Id || categoryRequest.CreatedCategoryId == request.Id,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "CATEGORY_HAS_REQUESTS",
                "لا يمكن حذف التصنيف لأنه مرتبط بطلبات كتالوج.|Cannot delete this category because it is linked to catalog requests.");
        }

        if (await _context.AdminBrandBulkOperationItems.AnyAsync(item => item.CategoryId == request.Id, cancellationToken)
            || await _context.AdminMasterProductBulkOperationItems.AnyAsync(item => item.CategoryId == request.Id, cancellationToken)
            || await _context.HomeSections.AnyAsync(section => section.CategoryId == request.Id, cancellationToken))
        {
            throw new BusinessRuleException(
                "CATEGORY_HAS_HISTORY",
                "لا يمكن حذف التصنيف لأنه مرتبط بسجلات أو أقسام عرض حالية.|Cannot delete this category because it is linked to history records or active display sections.");
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
    }
}
