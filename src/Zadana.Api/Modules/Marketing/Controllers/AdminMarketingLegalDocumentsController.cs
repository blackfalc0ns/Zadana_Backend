using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Authorization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Marketing.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Marketing.Controllers;

[Route("api/admin/marketing/legal-documents")]
[Authorize(Policy = "AdminOnly")]
[Tags("Marketing (Admins)")]
public sealed class AdminMarketingLegalDocumentsController : ApiControllerBase
{
    [HttpGet]
    [RequireAccess(PermissionKeys.Admin.MarketingView)]
    public async Task<ActionResult<List<PlatformLegalDocumentDto>>> List(
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        await EnsureAllDocumentTypesAsync(context, cancellationToken);

        var documents = await context.PlatformLegalDocuments
            .AsNoTracking()
            .OrderBy(item => item.DocumentType)
            .ToListAsync(cancellationToken);

        return Ok(documents.Select(Map).ToList());
    }

    [HttpGet("{documentType}")]
    [RequireAccess(PermissionKeys.Admin.MarketingView)]
    public async Task<ActionResult<PlatformLegalDocumentDto>> Get(
        string documentType,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (!TryParseDocumentType(documentType, out var type))
        {
            return BadRequest(new { detail = "Unknown legal document type." });
        }

        var document = await EnsureDocumentAsync(context, type, cancellationToken);
        return Ok(Map(document));
    }

    [HttpPut("{documentType}")]
    [RequireAccess(PermissionKeys.Admin.MarketingManageSettings)]
    public async Task<ActionResult<PlatformLegalDocumentDto>> Upsert(
        string documentType,
        [FromBody] UpsertPlatformLegalDocumentRequest request,
        [FromServices] IApplicationDbContext context,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        if (!TryParseDocumentType(documentType, out var type))
        {
            return BadRequest(new { detail = "Unknown legal document type." });
        }

        var actorId = currentUserService.UserId
            ?? throw new UnauthorizedException("ADMIN_NOT_AUTHENTICATED");

        var document = await context.PlatformLegalDocuments
            .FirstOrDefaultAsync(item => item.DocumentType == type, cancellationToken);

        if (document is null)
        {
            document = new PlatformLegalDocument(
                type,
                request.ContentAr,
                request.ContentEn,
                request.Version,
                request.EffectiveAtUtc,
                actorId);
            context.PlatformLegalDocuments.Add(document);
        }
        else
        {
            document.Update(
                request.ContentAr,
                request.ContentEn,
                request.Version,
                request.EffectiveAtUtc,
                actorId);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Ok(Map(document));
    }

    internal static bool TryParseDocumentType(string value, out PlatformLegalDocumentType documentType) =>
        Enum.TryParse(value, ignoreCase: true, out documentType) &&
        Enum.IsDefined(documentType);

    internal static async Task EnsureAllDocumentTypesAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var existing = await context.PlatformLegalDocuments
            .Select(item => item.DocumentType)
            .ToListAsync(cancellationToken);

        var added = false;
        foreach (PlatformLegalDocumentType type in Enum.GetValues<PlatformLegalDocumentType>())
        {
            if (existing.Contains(type))
            {
                continue;
            }

            context.PlatformLegalDocuments.Add(new PlatformLegalDocument(type));
            added = true;
        }

        if (added)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    internal static async Task<PlatformLegalDocument> EnsureDocumentAsync(
        IApplicationDbContext context,
        PlatformLegalDocumentType type,
        CancellationToken cancellationToken)
    {
        var document = await context.PlatformLegalDocuments
            .FirstOrDefaultAsync(item => item.DocumentType == type, cancellationToken);

        if (document is not null)
        {
            return document;
        }

        document = new PlatformLegalDocument(type);
        context.PlatformLegalDocuments.Add(document);
        await context.SaveChangesAsync(cancellationToken);
        return document;
    }

    internal static PlatformLegalDocumentDto Map(PlatformLegalDocument document) =>
        new(
            document.DocumentType.ToString(),
            document.ContentAr,
            document.ContentEn,
            document.Version,
            document.EffectiveAtUtc,
            document.UpdatedAtUtc);
}

[Route("api/public/legal")]
[AllowAnonymous]
[Tags("Public Legal Documents")]
public sealed class PublicLegalDocumentsController : ApiControllerBase
{
    [HttpGet("{documentType}")]
    public async Task<ActionResult<PlatformLegalDocumentDto>> Get(
        string documentType,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (!AdminMarketingLegalDocumentsController.TryParseDocumentType(documentType, out var type))
        {
            return BadRequest(new { detail = "Unknown legal document type." });
        }

        var document = await context.PlatformLegalDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DocumentType == type, cancellationToken);

        if (document is null)
        {
            return Ok(new PlatformLegalDocumentDto(
                type.ToString(),
                string.Empty,
                string.Empty,
                "1.0",
                DateTime.UtcNow.Date,
                DateTime.UtcNow));
        }

        return Ok(AdminMarketingLegalDocumentsController.Map(document));
    }
}
