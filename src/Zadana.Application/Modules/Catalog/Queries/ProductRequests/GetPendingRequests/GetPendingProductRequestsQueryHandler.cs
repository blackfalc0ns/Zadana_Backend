using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Extensions;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetPendingRequests;

public class GetPendingProductRequestsQueryHandler : IRequestHandler<GetPendingProductRequestsQuery, PaginatedList<AdminProductRequestDto>>
{
    private readonly IProductRequestReadService _productRequestReadService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public GetPendingProductRequestsQueryHandler(
        IProductRequestReadService productRequestReadService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer)
    {
        _productRequestReadService = productRequestReadService;
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    public async Task<PaginatedList<AdminProductRequestDto>> Handle(GetPendingProductRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.HasRole(UserRole.Admin, UserRole.SuperAdmin))
        {
            throw new ForbiddenAccessException(_localizer["UNAUTHORIZED_VIEW_REQUESTS"]);
        }

        return await _productRequestReadService.GetPendingAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
