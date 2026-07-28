using MediatR;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Vendors.Queries.GetAllVendors;

public record GetAllVendorsQuery(
    VendorStatus? Status,
    string? Search,
    string? City,
    string? Region,
    bool? IsLoginLocked,
    string? RiskLevel = null,
    string? VerificationStatus = null,
    string? DocumentsStatus = null,
    string? PayoutStatus = null,
    string? OnboardingStage = null,
    int Page = 1,
    int PageSize = 10) : IRequest<PaginatedList<VendorListItemDto>>;
