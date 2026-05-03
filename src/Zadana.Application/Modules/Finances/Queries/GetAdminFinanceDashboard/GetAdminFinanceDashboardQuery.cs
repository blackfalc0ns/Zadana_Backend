using MediatR;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Queries.GetAdminFinanceDashboard;

public record GetAdminFinanceDashboardQuery(string Period = "month") : IRequest<AdminFinanceDashboardDto>;
