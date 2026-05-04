using MediatR;
using Zadana.Application.Modules.Dashboard.DTOs;

namespace Zadana.Application.Modules.Dashboard.Queries.GetAdminDashboardOverview;

public sealed record GetAdminDashboardOverviewQuery(
    string Period = "today",
    string Region = "all",
    Guid? VendorId = null) : IRequest<AdminDashboardOverviewDto>;
