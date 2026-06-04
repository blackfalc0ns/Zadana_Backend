using MediatR;
using Zadana.Application.Modules.Geography.DTOs;

namespace Zadana.Application.Modules.Geography.Queries.GetAdminGeographyCoverage;

public sealed record GetAdminGeographyCoverageQuery(
    string Region = "all",
    bool GapsOnly = false) : IRequest<AdminGeographyCoverageDto>;
