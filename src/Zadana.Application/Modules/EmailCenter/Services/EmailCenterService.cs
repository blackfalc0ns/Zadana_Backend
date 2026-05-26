using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.EmailCenter;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.EmailCenter.Services;

public sealed class EmailCenterService : IEmailCenterService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string EmailLogoUrl = "https://ik.imagekit.io/fnyx4x87z/logo/%D8%B4%D9%81%D8%A7%D9%81%20(4).png";
    private static readonly IReadOnlyDictionary<string, string> LegacyEmailAddressMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ops@zadana.sa"] = "support@zadna0.com",
            ["support@zadana.sa"] = "support@zadna0.com",
            ["security@zadana.sa"] = "support@zadna0.com",
            ["access.control@zadana.sa"] = "support@zadna0.com",
            ["security.audit@zadana.sa"] = "contact@zadna0.com",
            ["vendors@zadana.sa"] = "contact@zadna0.com",
            ["vendor.success@zadana.sa"] = "hello@zadna0.com",
            ["ops.leads@zadana.sa"] = "support@zadna0.com",
            ["vendor.helpdesk@zadana.sa"] = "support@zadna0.com",
            ["finance@zadana.sa"] = "info@zadna0.com",
            ["settlements@zadana.sa"] = "info@zadna0.com",
            ["finance.control@zadana.sa"] = "contact@zadna0.com",
            ["driver.payments@zadana.sa"] = "info@zadna0.com"
        };

    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUserService;

    public EmailCenterService(
        IApplicationDbContext context,
        IEmailService emailService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _emailService = emailService;
        _currentUserService = currentUserService;
    }

    public async Task<EmailCenterOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(cancellationToken);

        var profiles = await _context.EmailSenderProfileConfigs
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var rules = await _context.EmailWorkflowRuleConfigs
            .AsNoTracking()
            .OrderBy(x => x.AudienceType)
            .ThenBy(x => x.TitleKey)
            .ToListAsync(cancellationToken);

        var latestDispatchByRule = await _context.EmailDispatchLogs
            .AsNoTracking()
            .Where(x => x.RuleKey != null)
            .GroupBy(x => x.RuleKey!)
            .Select(group => group
                .OrderByDescending(item => item.CreatedAtUtc)
                .Select(item => new EmailDispatchSummaryDto(
                    item.Status,
                    item.Source,
                    item.CreatedAtUtc,
                    item.FailureReason))
                .First())
            .ToListAsync(cancellationToken);

        var latestDispatchLookup = await _context.EmailDispatchLogs
            .AsNoTracking()
            .Where(x => x.RuleKey != null)
            .GroupBy(x => x.RuleKey!)
            .Select(group => new
            {
                RuleKey = group.Key,
                Item = group.OrderByDescending(item => item.CreatedAtUtc).First()
            })
            .ToDictionaryAsync(
                item => item.RuleKey,
                item => new EmailDispatchSummaryDto(
                    item.Item.Status,
                    item.Item.Source,
                    item.Item.CreatedAtUtc,
                    item.Item.FailureReason),
                cancellationToken);

        var vendors = await _context.Vendors
            .AsNoTracking()
            .OrderBy(x => x.BusinessNameEn)
            .Select(x => new EmailScopeOptionDto(
                x.Id.ToString(),
                string.IsNullOrWhiteSpace(x.BusinessNameEn) ? x.BusinessNameAr : x.BusinessNameEn))
            .ToListAsync(cancellationToken);

        var branches = await _context.VendorBranches
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new EmailBranchOptionDto(
                x.Id.ToString(),
                x.VendorId.ToString(),
                x.Name))
            .ToListAsync(cancellationToken);

        var ruleDtos = rules
            .Select(rule => MapRule(rule, latestDispatchLookup.GetValueOrDefault(rule.RuleKey)))
            .ToList();

        return new EmailCenterOverviewDto(
            profiles.Select(MapSenderProfile).ToList(),
            ruleDtos,
            new EmailCenterKpiSnapshotDto(
                ruleDtos.Count,
                ruleDtos.Count(x => x.Enabled),
                profiles.Count,
                ruleDtos.Count(x =>
                    x.RecipientTargets.To.Count > 0 ||
                    x.RecipientTargets.Cc.Count > 0 ||
                    x.RecipientTargets.Bcc.Count > 0),
                ruleDtos.Where(x => x.Enabled).Select(x => x.AudienceType).Distinct(StringComparer.OrdinalIgnoreCase).Count()),
            vendors,
            branches);
    }

    public async Task<EmailWorkflowRuleDto> UpdateRuleAsync(
        string ruleId,
        EmailWorkflowRuleDto draft,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(cancellationToken);

        var normalized = NormalizeRule(ruleId, draft);
        var entity = await _context.EmailWorkflowRuleConfigs
            .FirstOrDefaultAsync(x => x.RuleKey == normalized.Id, cancellationToken)
            ?? throw new NotFoundException("EmailWorkflowRule", ruleId);

        EnsureSenderProfileExists(normalized.SenderProfileId);

        entity.Update(
            normalized.TitleKey,
            normalized.SubtitleKey,
            normalized.CategoryKey,
            normalized.CadenceLabelKey,
            normalized.TriggerNotesKey,
            normalized.Enabled,
            normalized.SenderProfileId,
            normalized.AudienceType,
            normalized.PanelScope,
            Serialize(normalized.PersonaTargets),
            Serialize(normalized.EntityScope),
            normalized.BranchScopeMode,
            Serialize(normalized.RecipientTargets),
            Serialize(normalized.Route),
            Serialize(normalized.Template),
            normalized.AutomationState,
            normalized.EventKey,
            _currentUserService.UserId);

        await _context.SaveChangesAsync(cancellationToken);

        var latestDispatch = await _context.EmailDispatchLogs
            .AsNoTracking()
            .Where(x => x.RuleKey == normalized.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new EmailDispatchSummaryDto(x.Status, x.Source, x.CreatedAtUtc, x.FailureReason))
            .FirstOrDefaultAsync(cancellationToken);

        return MapRule(entity, latestDispatch);
    }

    public async Task<EmailResolvedRecipientsDto> ResolveRecipientsAsync(
        string ruleId,
        EmailWorkflowRuleDto draft,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(cancellationToken);
        var normalized = NormalizeRule(ruleId, draft);
        return await ResolveRuleRecipientsAsync(normalized, strictScope: false, runtimeVendor: null, null, cancellationToken);
    }

    public async Task<EmailTestSendResultDto> TestSendAsync(
        string ruleId,
        EmailWorkflowRuleDto draft,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(cancellationToken);
        var normalized = NormalizeRule(ruleId, draft);
        EnsureStrictScope(normalized);

        var resolved = await ResolveRuleRecipientsAsync(normalized, strictScope: true, runtimeVendor: null, null, cancellationToken);
        var recipients = CombineRecipients(resolved);
        if (recipients.Count == 0)
        {
            throw new BusinessRuleException("EMAIL_CENTER_NO_RECIPIENTS", "No recipients were resolved for this email rule.");
        }

        var senderProfile = await GetSenderProfileAsync(normalized.SenderProfileId, cancellationToken);
        var subject = normalized.Template.Subject.GetValueOrDefault("en")?.Trim();
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new BusinessRuleException("EMAIL_CENTER_SUBJECT_REQUIRED", "The email subject is required for test send.");
        }

        var sendResult = await SendEmailSafelyAsync(
            BuildManagedEmailRequest(normalized, senderProfile, resolved, targetUrl: "/email-center"),
            cancellationToken);

        var dispatchLog = CreateDispatchLog(
            rule: normalized,
            resolved: resolved,
            subject: subject,
            source: "test_send",
            sendResult: sendResult,
            reasonOverride: sendResult.FailureReason,
            eventKey: normalized.EventKey,
            entityId: ParseGuid(normalized.EntityScope.EntityId),
            vendorId: ParseGuid(normalized.EntityScope.VendorId),
            branchId: ParseGuid(normalized.EntityScope.BranchId),
            isTestSend: true);

        _context.EmailDispatchLogs.Add(dispatchLog);
        await _context.SaveChangesAsync(cancellationToken);

        return new EmailTestSendResultDto(
            dispatchLog.Id,
            dispatchLog.Status,
            dispatchLog.Provider,
            dispatchLog.ProviderMessageId,
            dispatchLog.FailureReason,
            dispatchLog.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<EmailDispatchLogDto>> GetDispatchesAsync(
        string? ruleId,
        string? source,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(cancellationToken);

        var query = _context.EmailDispatchLogs
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(ruleId))
        {
            var normalizedRuleId = ruleId.Trim();
            query = query.Where(x => x.RuleKey == normalizedRuleId);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            var normalizedSource = source.Trim().ToLowerInvariant();
            query = query.Where(x => x.Source == normalizedSource);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (dateFrom.HasValue)
        {
            var start = dateFrom.Value.Date.ToUniversalTime();
            query = query.Where(x => x.CreatedAtUtc >= start);
        }

        if (dateTo.HasValue)
        {
            var end = dateTo.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
            query = query.Where(x => x.CreatedAtUtc <= end);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return items.Select(MapDispatchLog).ToList();
    }

    public async Task<EmailDispatchOperationResult> DispatchSystemEventEmailAsync(
        EmailSystemEventDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(cancellationToken);

        var eventKey = NormalizeOptional(request.EventKey);
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            return new EmailDispatchOperationResult(false, false, true, request.Source, null, null, "Email event key is required.");
        }

        var source = string.IsNullOrWhiteSpace(request.Source) ? "system_event" : request.Source.Trim().ToLowerInvariant();
        var audienceType = string.IsNullOrWhiteSpace(request.AudienceType) ? "system" : request.AudienceType.Trim();

        var ruleEntity = await _context.EmailWorkflowRuleConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.EventKey != null && x.EventKey == eventKey,
                cancellationToken);

        if (ruleEntity is null)
        {
            return await LogSystemEventSkipAsync(
                rule: null,
                ruleKey: null,
                ruleLabel: HumanizeEventKey(eventKey),
                audienceType: audienceType,
                eventKey: eventKey,
                source: source,
                entityId: request.EntityId,
                vendorId: request.VendorId,
                branchId: request.BranchId,
                subject: HumanizeEventKey(eventKey),
                reason: "No live email rule is configured for this event.",
                cancellationToken);
        }

        var rule = MapRule(ruleEntity, lastDispatch: null);
        if (!rule.Enabled)
        {
            return await LogSystemEventSkipAsync(rule, eventKey, source, request, "Email automation rule is disabled.", cancellationToken);
        }

        if (!string.Equals(rule.AutomationState, "live", StringComparison.OrdinalIgnoreCase))
        {
            return await LogSystemEventSkipAsync(rule, eventKey, source, request, "Email automation is set to manual only.", cancellationToken);
        }

        if (!EmailEventKeys.LiveEmailEvents.Contains(eventKey))
        {
            return await LogSystemEventSkipAsync(rule, eventKey, source, request, "Email event is outside the reduced live email policy.", cancellationToken);
        }

        if (await HasDuplicateDispatchAsync(
                eventKey,
                request.EntityId,
                request.VendorId,
                request.BranchId,
                request.DuplicateWindowStartUtc,
                request.DuplicateWindowEndUtc,
                cancellationToken))
        {
            return await LogSystemEventSkipAsync(rule, eventKey, source, request, "Duplicate email event already has a dispatch log.", cancellationToken);
        }

        var resolved = new EmailResolvedRecipientsDto(
            NormalizeEmails(request.To ?? Array.Empty<string>()),
            NormalizeEmails(request.Cc ?? Array.Empty<string>()),
            NormalizeEmails(request.Bcc ?? Array.Empty<string>()),
            []);

        if (CombineRecipients(resolved).Count == 0)
        {
            return await LogSystemEventSkipAsync(rule, eventKey, source, request, "No recipients were resolved for this email event.", cancellationToken);
        }

        var senderProfile = await GetSenderProfileAsync(rule.SenderProfileId, cancellationToken);
        var variables = NormalizeVariables(request.Variables);
        var targetUrl = RenderTemplate(request.TargetUrl ?? string.Empty, variables);
        var sendResult = await SendEmailSafelyAsync(
            BuildManagedEmailRequest(
                rule,
                senderProfile,
                resolved,
                string.IsNullOrWhiteSpace(targetUrl) ? null : targetUrl,
                variables),
            cancellationToken);

        var subject = RenderTemplate(
            rule.Template.Subject.GetValueOrDefault("en") ??
            rule.Template.Subject.GetValueOrDefault("ar") ??
            rule.TitleKey,
            variables);

        var dispatchLog = CreateDispatchLog(
            rule: rule,
            resolved: resolved,
            subject: subject,
            source: source,
            sendResult: sendResult,
            reasonOverride: sendResult.FailureReason,
            eventKey: eventKey,
            entityId: request.EntityId,
            vendorId: request.VendorId,
            branchId: request.BranchId,
            isTestSend: false);

        _context.EmailDispatchLogs.Add(dispatchLog);
        await _context.SaveChangesAsync(cancellationToken);

        return new EmailDispatchOperationResult(
            Attempted: true,
            Sent: sendResult.Success,
            Skipped: false,
            Source: source,
            Provider: sendResult.Provider,
            ProviderMessageId: sendResult.ProviderMessageId,
            Reason: sendResult.FailureReason);
    }

    public async Task<EmailDispatchOperationResult> DispatchVendorEmailAsync(
        Vendor vendor,
        VendorCommunicationMessage message,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(cancellationToken);

        var eventKey = string.IsNullOrWhiteSpace(message.EmailEventKey)
            ? message.Type
            : message.EmailEventKey.Trim();

        if (!string.Equals(eventKey, EmailEventKeys.VendorApproved, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(eventKey, EmailEventKeys.VendorPasswordReset, StringComparison.OrdinalIgnoreCase))
        {
            return await LogVendorLifecycleSkipAsync(
                vendor,
                eventKey,
                "Vendor lifecycle email is outside the reduced live email policy.",
                cancellationToken);
        }

        var ruleEntity = await _context.EmailWorkflowRuleConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AudienceType == "vendor_network" &&
                     x.EventKey != null &&
                     x.EventKey == eventKey,
                cancellationToken);

        if (ruleEntity is null)
        {
            var defaultProfile = await GetDefaultSenderProfileAsync(cancellationToken);
            var legacyRecipients = new EmailResolvedRecipientsDto(
                NormalizeEmails([ResolveVendorEmail(vendor)]),
                [],
                [],
                []);

            if (legacyRecipients.To.Count == 0)
            {
                var skippedLog = CreateDispatchLog(
                    rule: null,
                    ruleKey: null,
                    ruleLabel: HumanizeEventKey(eventKey),
                    audienceType: "vendor_network",
                    resolved: legacyRecipients,
                    subject: message.TitleEn,
                    source: "vendor_automation_legacy",
                    sendResult: new EmailSendResult("resend", false, null, "Vendor has no email address for lifecycle communication."),
                    reasonOverride: "Vendor has no email address for lifecycle communication.",
                    eventKey: eventKey,
                    entityId: vendor.UserId,
                    vendorId: vendor.Id,
                    branchId: null,
                    isTestSend: false);

                _context.EmailDispatchLogs.Add(skippedLog);
                await _context.SaveChangesAsync(cancellationToken);

                return new EmailDispatchOperationResult(
                    Attempted: false,
                    Sent: false,
                    Skipped: true,
                    Source: "vendor_automation_legacy",
                    Provider: null,
                    ProviderMessageId: null,
                    Reason: "Vendor has no email address for lifecycle communication.");
            }

            var legacySendResult = await SendEmailSafelyAsync(
                BuildLegacyVendorEmailRequest(vendor, message, defaultProfile, legacyRecipients),
                cancellationToken);

            var legacyLog = CreateDispatchLog(
                rule: null,
                ruleKey: null,
                ruleLabel: HumanizeEventKey(eventKey),
                audienceType: "vendor_network",
                resolved: legacyRecipients,
                subject: message.TitleEn,
                source: "vendor_automation_legacy",
                sendResult: legacySendResult,
                reasonOverride: legacySendResult.FailureReason,
                eventKey: eventKey,
                entityId: vendor.UserId,
                vendorId: vendor.Id,
                branchId: null,
                isTestSend: false);

            _context.EmailDispatchLogs.Add(legacyLog);
            await _context.SaveChangesAsync(cancellationToken);

            return new EmailDispatchOperationResult(
                Attempted: true,
                Sent: legacySendResult.Success,
                Skipped: false,
                Source: "vendor_automation_legacy",
                Provider: legacySendResult.Provider,
                ProviderMessageId: legacySendResult.ProviderMessageId,
                Reason: legacySendResult.FailureReason);
        }

        var rule = MapRule(ruleEntity, lastDispatch: null);
        var effectiveRule = ApplyVendorRuntimeScope(rule, vendor);

        if (!effectiveRule.Enabled)
        {
            await LogAutomationSkipAsync(effectiveRule, vendor, eventKey, "Email automation rule is disabled.", cancellationToken);
            return new EmailDispatchOperationResult(false, false, true, "vendor_automation_live", null, null, "Email automation rule is disabled.");
        }

        if (!string.Equals(effectiveRule.AutomationState, "live", StringComparison.OrdinalIgnoreCase))
        {
            await LogAutomationSkipAsync(effectiveRule, vendor, eventKey, "Email automation is set to manual only.", cancellationToken);
            return new EmailDispatchOperationResult(false, false, true, "vendor_automation_live", null, null, "Email automation is set to manual only.");
        }

        if (!string.IsNullOrWhiteSpace(rule.EntityScope.VendorId) &&
            !string.Equals(rule.EntityScope.VendorId, vendor.Id.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            await LogAutomationSkipAsync(effectiveRule, vendor, eventKey, "The live rule is scoped to another vendor.", cancellationToken);
            return new EmailDispatchOperationResult(false, false, true, "vendor_automation_live", null, null, "The live rule is scoped to another vendor.");
        }

        var resolved = await ResolveRuleRecipientsAsync(
            effectiveRule,
            strictScope: true,
            runtimeVendor: vendor,
            message,
            cancellationToken);

        var allRecipients = CombineRecipients(resolved);
        if (allRecipients.Count == 0)
        {
            await LogAutomationSkipAsync(effectiveRule, vendor, eventKey, "No recipients were resolved for the live automation rule.", cancellationToken);
            return new EmailDispatchOperationResult(false, false, true, "vendor_automation_live", null, null, "No recipients were resolved for the live automation rule.");
        }

        var senderProfile = await GetSenderProfileAsync(effectiveRule.SenderProfileId, cancellationToken);
        var sendResult = await SendEmailSafelyAsync(
            BuildManagedEmailRequest(
                effectiveRule,
                senderProfile,
                resolved,
                message.TargetUrl,
                BuildVendorTemplateVariables(vendor, message)),
            cancellationToken);

        var dispatchLog = CreateDispatchLog(
            rule: effectiveRule,
            resolved: resolved,
            subject: RenderTemplate(effectiveRule.Template.Subject.GetValueOrDefault("en") ?? message.TitleEn, BuildVendorTemplateVariables(vendor, message)),
            source: "vendor_automation_live",
            sendResult: sendResult,
            reasonOverride: sendResult.FailureReason,
            eventKey: eventKey,
            entityId: vendor.UserId,
            vendorId: vendor.Id,
            branchId: ParseGuid(effectiveRule.EntityScope.BranchId),
            isTestSend: false);

        _context.EmailDispatchLogs.Add(dispatchLog);
        await _context.SaveChangesAsync(cancellationToken);

        return new EmailDispatchOperationResult(
            Attempted: true,
            Sent: sendResult.Success,
            Skipped: false,
            Source: "vendor_automation_live",
            Provider: sendResult.Provider,
            ProviderMessageId: sendResult.ProviderMessageId,
            Reason: sendResult.FailureReason);
    }

    private async Task EnsureSeedDataAsync(CancellationToken cancellationToken)
    {
        var defaultProfiles = EmailCenterDefaults.BuildSenderProfiles();
        var existingProfiles = await _context.EmailSenderProfileConfigs
            .ToDictionaryAsync(x => x.ProfileKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var defaultProfile in defaultProfiles)
        {
            if (existingProfiles.TryGetValue(defaultProfile.ProfileKey, out var existingProfile))
            {
                if (existingProfile.IsReadOnly)
                {
                    existingProfile.UpdateSystemDefaults(
                        defaultProfile.Name,
                        defaultProfile.Address,
                        defaultProfile.ReplyTo,
                        defaultProfile.DescriptionKey,
                        defaultProfile.Locale,
                        defaultProfile.IsDefault,
                        defaultProfile.Status);
                }

                continue;
            }

            _context.EmailSenderProfileConfigs.Add(defaultProfile);
        }

        if (!await _context.EmailWorkflowRuleConfigs.AnyAsync(cancellationToken))
        {
            _context.EmailWorkflowRuleConfigs.AddRange(EmailCenterDefaults.BuildWorkflowRules());
        }

        await SyncReducedEmailRulesAsync(cancellationToken);
        await SyncLegacyEmailRuleAddressesAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncReducedEmailRulesAsync(CancellationToken cancellationToken)
    {
        var defaults = EmailCenterDefaults.BuildWorkflowRules()
            .ToDictionary(rule => rule.RuleKey, StringComparer.OrdinalIgnoreCase);
        var rules = await _context.EmailWorkflowRuleConfigs.ToListAsync(cancellationToken);
        var existingByKey = rules.ToDictionary(rule => rule.RuleKey, StringComparer.OrdinalIgnoreCase);

        foreach (var defaultRule in defaults.Values)
        {
            if (existingByKey.TryGetValue(defaultRule.RuleKey, out var existingRule))
            {
                existingRule.Update(
                    defaultRule.TitleKey,
                    defaultRule.SubtitleKey,
                    defaultRule.CategoryKey,
                    defaultRule.CadenceLabelKey,
                    defaultRule.TriggerNotesKey,
                    defaultRule.Enabled,
                    defaultRule.SenderProfileKey,
                    defaultRule.AudienceType,
                    defaultRule.PanelScope,
                    defaultRule.PersonaTargetsJson,
                    defaultRule.EntityScopeJson,
                    defaultRule.BranchScopeMode,
                    defaultRule.RecipientTargetsJson,
                    defaultRule.RouteJson,
                    defaultRule.TemplateJson,
                    defaultRule.AutomationState,
                    defaultRule.EventKey,
                    existingRule.UpdatedByUserId);
                continue;
            }

            _context.EmailWorkflowRuleConfigs.Add(defaultRule);
            rules.Add(defaultRule);
        }

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.EventKey) ||
                EmailEventKeys.LiveEmailEvents.Contains(rule.EventKey))
            {
                continue;
            }

            if (!rule.Enabled &&
                string.Equals(rule.AutomationState, "manual_only", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            rule.Update(
                rule.TitleKey,
                rule.SubtitleKey,
                rule.CategoryKey,
                rule.CadenceLabelKey,
                rule.TriggerNotesKey,
                false,
                rule.SenderProfileKey,
                rule.AudienceType,
                rule.PanelScope,
                rule.PersonaTargetsJson,
                rule.EntityScopeJson,
                rule.BranchScopeMode,
                rule.RecipientTargetsJson,
                rule.RouteJson,
                rule.TemplateJson,
                "manual_only",
                rule.EventKey,
                rule.UpdatedByUserId);
        }
    }

    private async Task SyncLegacyEmailRuleAddressesAsync(CancellationToken cancellationToken)
    {
        var rules = await _context.EmailWorkflowRuleConfigs.ToListAsync(cancellationToken);
        foreach (var rule in rules)
        {
            var routeJson = ReplaceLegacyEmailAddresses(rule.RouteJson);
            if (string.Equals(routeJson, rule.RouteJson, StringComparison.Ordinal))
            {
                continue;
            }

            rule.Update(
                rule.TitleKey,
                rule.SubtitleKey,
                rule.CategoryKey,
                rule.CadenceLabelKey,
                rule.TriggerNotesKey,
                rule.Enabled,
                rule.SenderProfileKey,
                rule.AudienceType,
                rule.PanelScope,
                rule.PersonaTargetsJson,
                rule.EntityScopeJson,
                rule.BranchScopeMode,
                rule.RecipientTargetsJson,
                routeJson,
                rule.TemplateJson,
                rule.AutomationState,
                rule.EventKey,
                rule.UpdatedByUserId);
        }
    }

    private static string ReplaceLegacyEmailAddresses(string value)
    {
        var result = value;
        foreach (var (legacyEmail, currentEmail) in LegacyEmailAddressMap)
        {
            result = result.Replace(legacyEmail, currentEmail, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private void EnsureStrictScope(EmailWorkflowRuleDto rule)
    {
        switch (rule.AudienceType)
        {
            case "super_admin":
            case "drivers":
            case "customers":
                if (ParseGuid(rule.EntityScope.EntityId) is null)
                {
                    throw new BusinessRuleException("EMAIL_CENTER_SCOPE_REQUIRED", "A specific entityId is required for this audience.");
                }
                break;
            case "vendor_network":
                if (ParseGuid(rule.EntityScope.VendorId) is null)
                {
                    throw new BusinessRuleException("EMAIL_CENTER_SCOPE_REQUIRED", "A specific vendorId is required for vendor network test send.");
                }

                if (string.Equals(rule.BranchScopeMode, "specific_branch", StringComparison.OrdinalIgnoreCase) &&
                    ParseGuid(rule.EntityScope.BranchId) is null)
                {
                    throw new BusinessRuleException("EMAIL_CENTER_SCOPE_REQUIRED", "A specific branchId is required when branch scope is specific_branch.");
                }
                break;
            default:
                throw new BusinessRuleException("EMAIL_CENTER_INVALID_AUDIENCE", "The selected audience is not supported.");
        }
    }

    private async Task<EmailResolvedRecipientsDto> ResolveRuleRecipientsAsync(
        EmailWorkflowRuleDto rule,
        bool strictScope,
        Vendor? runtimeVendor,
        VendorCommunicationMessage? runtimeMessage,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        if (!strictScope && HasAmbiguousScope(rule))
        {
            warnings.Add(GetAmbiguousScopeMessage(rule));
        }

        if (strictScope)
        {
            EnsureStrictScope(rule);
        }

        var directory = await LoadDirectoryRecipientsAsync(rule.AudienceType, cancellationToken);
        var scoped = directory
            .Where(entry => MatchesScope(entry, rule))
            .Where(entry => rule.PersonaTargets.Count == 0 || rule.PersonaTargets.Contains(entry.PersonaType))
            .ToList();

        var relatedRegion = runtimeVendor?.Region ?? scoped.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Region))?.Region;

        var to = MergeRecipients(
            ResolveTargetEmails(rule, scoped, directory, rule.RecipientTargets.To, relatedRegion),
            rule.Route.StaticTo,
            rule.Route.FallbackTo);

        var cc = MergeRecipients(
            ResolveTargetEmails(rule, scoped, directory, rule.RecipientTargets.Cc, relatedRegion),
            rule.Route.StaticCc,
            rule.Route.FallbackCc);

        var bcc = MergeRecipients(
            ResolveTargetEmails(rule, scoped, directory, rule.RecipientTargets.Bcc, relatedRegion),
            rule.Route.StaticBcc,
            rule.Route.FallbackBcc);

        if (strictScope && to.Count + cc.Count + bcc.Count == 0)
        {
            throw new BusinessRuleException("EMAIL_CENTER_NO_RECIPIENTS", "No recipients were resolved for this rule.");
        }

        if (to.Count + cc.Count + bcc.Count == 0)
        {
            warnings.Add("No recipients are currently resolved for this scope.");
        }

        return new EmailResolvedRecipientsDto(to, cc, bcc, warnings);
    }

    private async Task<List<DirectoryRecipientRecord>> LoadDirectoryRecipientsAsync(string audienceType, CancellationToken cancellationToken)
    {
        return audienceType switch
        {
            "super_admin" => await LoadAdminRecipientsAsync(cancellationToken),
            "vendor_network" => await LoadVendorRecipientsAsync(cancellationToken),
            "drivers" => await LoadDriverRecipientsAsync(cancellationToken),
            "customers" => await LoadCustomerRecipientsAsync(cancellationToken),
            _ => []
        };
    }

    private async Task<List<DirectoryRecipientRecord>> LoadAdminRecipientsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from scope in _context.UserAccessScopes.AsNoTracking()
            join user in _context.Users.AsNoTracking() on scope.UserId equals user.Id
            join role in _context.RoleDefinitions.AsNoTracking() on scope.RoleDefinitionId equals role.Id
            where scope.IsActive &&
                  scope.PanelScope == PanelScope.SuperAdminPanel &&
                  !string.IsNullOrWhiteSpace(user.Email) &&
                  user.AccountStatus == AccountStatus.Active
            select new { user, role })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new DirectoryRecipientRecord(
            row.user.Id,
            row.user.Email!,
            row.role.Code == "super_admin_all" ? "super_admin_manager" : "super_admin_staff",
            "super_admin",
            "super_admin_panel",
            row.user.Id,
            null,
            null,
            null)).ToList();
    }

    private async Task<List<DirectoryRecipientRecord>> LoadVendorRecipientsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from scope in _context.UserAccessScopes.AsNoTracking()
            join user in _context.Users.AsNoTracking() on scope.UserId equals user.Id
            join role in _context.RoleDefinitions.AsNoTracking() on scope.RoleDefinitionId equals role.Id
            join branch in _context.VendorBranches.AsNoTracking() on scope.ScopeEntityId equals branch.Id into branchGroup
            from branch in branchGroup.DefaultIfEmpty()
            join vendor in _context.Vendors.AsNoTracking() on
                (scope.ScopeType == AccessScopeType.VendorCompany ? scope.ScopeEntityId : branch.VendorId) equals vendor.Id
            where scope.IsActive &&
                  scope.PanelScope == PanelScope.VendorPanel &&
                  !string.IsNullOrWhiteSpace(user.Email) &&
                  user.AccountStatus == AccountStatus.Active
            select new { user, role, scope, vendor, branch })
            .ToListAsync(cancellationToken);

        var results = new List<DirectoryRecipientRecord>();

        foreach (var row in rows)
        {
            var personaType = row.role.Code switch
            {
                "vendor_owner" => "vendor_owner",
                "vendor_branch_manager" => "vendor_branch_manager",
                _ => "vendor_branch_employee"
            };

            results.Add(new DirectoryRecipientRecord(
                row.user.Id,
                row.user.Email!,
                personaType,
                "vendor_network",
                "vendor_panel",
                row.scope.ScopeEntityId,
                row.vendor.Id,
                row.branch?.Id,
                NormalizeRegion(row.vendor.Region)));
        }

        return results;
    }

    private async Task<List<DirectoryRecipientRecord>> LoadDriverRecipientsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from driver in _context.Drivers.AsNoTracking()
            join user in _context.Users.AsNoTracking() on driver.UserId equals user.Id
            where !string.IsNullOrWhiteSpace(user.Email) &&
                  user.AccountStatus == AccountStatus.Active
            select new { driver, user })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new DirectoryRecipientRecord(
            row.user.Id,
            row.user.Email!,
            "driver",
            "drivers",
            "driver_app",
            row.driver.Id,
            null,
            null,
            NormalizeRegion(row.driver.Region))).ToList();
    }

    private async Task<List<DirectoryRecipientRecord>> LoadCustomerRecipientsAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.Users
            .AsNoTracking()
            .Where(user =>
                user.Role == UserRole.Customer &&
                !string.IsNullOrWhiteSpace(user.Email) &&
                user.AccountStatus == AccountStatus.Active)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new DirectoryRecipientRecord(
            row.Id,
            row.Email!,
            "customer",
            "customers",
            "customer_app",
            row.Id,
            null,
            null,
            null)).ToList();
    }

    private static bool MatchesScope(DirectoryRecipientRecord entry, EmailWorkflowRuleDto rule)
    {
        if (!string.Equals(entry.AudienceType, rule.AudienceType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(entry.PanelScope, rule.PanelScope, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ParseGuid(rule.EntityScope.EntityId) is Guid entityId && entry.EntityId != entityId)
        {
            return false;
        }

        if (ParseGuid(rule.EntityScope.VendorId) is Guid vendorId && entry.VendorId != vendorId)
        {
            return false;
        }

        if (string.Equals(rule.BranchScopeMode, "specific_branch", StringComparison.OrdinalIgnoreCase) &&
            ParseGuid(rule.EntityScope.BranchId) is Guid branchId &&
            entry.BranchId != branchId)
        {
            return false;
        }

        return true;
    }

    private static List<string> ResolveTargetEmails(
        EmailWorkflowRuleDto rule,
        IReadOnlyList<DirectoryRecipientRecord> scoped,
        IReadOnlyList<DirectoryRecipientRecord> all,
        IReadOnlyList<string> targetIds,
        string? relatedRegion)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var targetId in targetIds)
        {
            switch (targetId)
            {
                case "primary_account_email":
                    foreach (var item in scoped)
                    {
                        result.Add(item.Email);
                    }
                    break;
                case "vendor_owner":
                    AddByPersona(result, scoped, "vendor_owner");
                    break;
                case "vendor_company_manager":
                    AddByCompanyScopedVendor(result, scoped);
                    break;
                case "branch_manager":
                    AddByPersona(result, scoped, "vendor_branch_manager");
                    break;
                case "branch_staff":
                    AddByPersona(result, scoped, "vendor_branch_employee");
                    break;
                case "vendor_finance":
                case "vendor_support":
                    AddByCompanyScopedVendor(result, scoped);
                    break;
                case "assigned_super_admin_manager":
                    foreach (var item in all.Where(x =>
                                 x.AudienceType == "super_admin" &&
                                 (string.IsNullOrWhiteSpace(relatedRegion) ||
                                  string.IsNullOrWhiteSpace(x.Region) ||
                                  string.Equals(x.Region, relatedRegion, StringComparison.OrdinalIgnoreCase))))
                    {
                        result.Add(item.Email);
                    }
                    break;
                case "driver_account":
                    AddByPersona(result, scoped, "driver");
                    break;
                case "customer_account":
                    AddByPersona(result, scoped, "customer");
                    break;
            }
        }

        return result.ToList();
    }

    private static void AddByPersona(HashSet<string> destination, IEnumerable<DirectoryRecipientRecord> source, string personaType)
    {
        foreach (var item in source.Where(x => x.PersonaType == personaType))
        {
            destination.Add(item.Email);
        }
    }

    private static void AddByCompanyScopedVendor(HashSet<string> destination, IEnumerable<DirectoryRecipientRecord> source)
    {
        foreach (var item in source.Where(x => x.AudienceType == "vendor_network" && x.BranchId == null))
        {
            destination.Add(item.Email);
        }
    }

    private static List<string> MergeRecipients(
        IReadOnlyList<string> dynamicRecipients,
        IReadOnlyList<string> staticRecipients,
        IReadOnlyList<string> fallbackRecipients)
    {
        var merged = new HashSet<string>(dynamicRecipients.Concat(staticRecipients).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant()));
        if (merged.Count > 0)
        {
            return merged.ToList();
        }

        return fallbackRecipients
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasAmbiguousScope(EmailWorkflowRuleDto rule)
    {
        return rule.AudienceType switch
        {
            "super_admin" or "drivers" or "customers" => ParseGuid(rule.EntityScope.EntityId) is null,
            "vendor_network" => ParseGuid(rule.EntityScope.VendorId) is null ||
                                (string.Equals(rule.BranchScopeMode, "specific_branch", StringComparison.OrdinalIgnoreCase) &&
                                 ParseGuid(rule.EntityScope.BranchId) is null),
            _ => true
        };
    }

    private static string GetAmbiguousScopeMessage(EmailWorkflowRuleDto rule)
    {
        return rule.AudienceType switch
        {
            "super_admin" => "Preview is limited until a specific admin entityId is provided.",
            "drivers" => "Preview is limited until a specific driver entityId is provided.",
            "customers" => "Preview is limited until a specific customer entityId is provided.",
            "vendor_network" when string.Equals(rule.BranchScopeMode, "specific_branch", StringComparison.OrdinalIgnoreCase)
                => "Preview is limited until both vendorId and branchId are selected.",
            "vendor_network" => "Preview is limited until a specific vendorId is selected.",
            _ => "Preview is limited until the email scope is fully defined."
        };
    }

    private async Task<EmailSenderProfileDto> GetSenderProfileAsync(string senderProfileId, CancellationToken cancellationToken)
    {
        var profile = await _context.EmailSenderProfileConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProfileKey == senderProfileId, cancellationToken);

        if (profile is not null)
        {
            return MapSenderProfile(profile);
        }

        var fallback = await _context.EmailSenderProfileConfigs
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .FirstAsync(cancellationToken);

        return MapSenderProfile(fallback);
    }

    private async Task<EmailSenderProfileDto> GetDefaultSenderProfileAsync(CancellationToken cancellationToken)
    {
        var profile = await _context.EmailSenderProfileConfigs
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .FirstAsync(cancellationToken);

        return MapSenderProfile(profile);
    }

    private async Task<EmailSendResult> SendEmailSafelyAsync(
        SendEmailRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _emailService.SendEmailAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new EmailSendResult("unknown", false, null, ex.Message);
        }
    }

    private void EnsureSenderProfileExists(string senderProfileId)
    {
        if (!_context.EmailSenderProfileConfigs.Any(x => x.ProfileKey == senderProfileId))
        {
            throw new BusinessRuleException("EMAIL_CENTER_INVALID_SENDER_PROFILE", "The selected sender profile does not exist.");
        }
    }

    private static SendEmailRequest BuildManagedEmailRequest(
        EmailWorkflowRuleDto rule,
        EmailSenderProfileDto senderProfile,
        EmailResolvedRecipientsDto recipients,
        string? targetUrl,
        IReadOnlyDictionary<string, string>? variables = null)
    {
        var subjectEn = RenderTemplate(rule.Template.Subject.GetValueOrDefault("en") ?? string.Empty, variables);
        var bodyEn = RenderTemplate(rule.Template.Body.GetValueOrDefault("en") ?? string.Empty, variables);
        var subjectAr = RenderTemplate(rule.Template.Subject.GetValueOrDefault("ar") ?? string.Empty, variables);
        var bodyAr = RenderTemplate(rule.Template.Body.GetValueOrDefault("ar") ?? string.Empty, variables);

        return new SendEmailRequest(
            recipients.To.ToArray(),
            string.IsNullOrWhiteSpace(subjectEn) ? subjectAr : subjectEn,
            BuildBilingualEmailHtml(
                subjectEn,
                bodyEn,
                subjectAr,
                bodyAr,
                targetUrl,
                rule.Template.HeroImageUrl,
                rule.Template.CtaLabel,
                rule.Template.HeroImageUrlAr,
                rule.Template.HeroImageUrlEn),
            From: $"{senderProfile.Name} <{senderProfile.Address}>",
            ReplyTo: senderProfile.ReplyTo,
            Cc: recipients.Cc.ToArray(),
            Bcc: recipients.Bcc.ToArray(),
            Metadata: new Dictionary<string, string>
            {
                ["ruleId"] = rule.Id,
                ["audience"] = rule.AudienceType,
                ["automationState"] = rule.AutomationState
            });
    }

    private static SendEmailRequest BuildLegacyVendorEmailRequest(
        Vendor vendor,
        VendorCommunicationMessage message,
        EmailSenderProfileDto senderProfile,
        EmailResolvedRecipientsDto recipients)
    {
        return new SendEmailRequest(
            recipients.To.ToArray(),
            message.TitleEn,
            BuildBilingualEmailHtml(message.TitleEn, message.BodyEn, message.TitleAr, message.BodyAr, message.TargetUrl),
            From: $"{senderProfile.Name} <{senderProfile.Address}>",
            ReplyTo: senderProfile.ReplyTo,
            Cc: recipients.Cc.ToArray(),
            Bcc: recipients.Bcc.ToArray(),
            Metadata: new Dictionary<string, string>
            {
                ["vendorId"] = vendor.Id.ToString(),
                ["eventKey"] = message.EmailEventKey ?? message.Type
            });
    }

    private static string BuildBilingualEmailHtml(
        string titleEn,
        string bodyEn,
        string titleAr,
        string bodyAr,
        string? targetUrl,
        string? heroImageUrl = null,
        string? ctaLabel = null,
        string? heroImageUrlAr = null,
        string? heroImageUrlEn = null)
    {
        var actionUrl = string.IsNullOrWhiteSpace(targetUrl) ? null : targetUrl.Trim();
        var fallbackHeroUrl = string.IsNullOrWhiteSpace(heroImageUrl) ? null : heroImageUrl.Trim();
        var heroUrlEn = string.IsNullOrWhiteSpace(heroImageUrlEn) ? fallbackHeroUrl : heroImageUrlEn.Trim();
        var heroUrlAr = string.IsNullOrWhiteSpace(heroImageUrlAr) ? fallbackHeroUrl : heroImageUrlAr.Trim();
        var actionLabel = string.IsNullOrWhiteSpace(ctaLabel) ? "Open related workspace" : ctaLabel.Trim();
        var builder = new StringBuilder();
        builder.Append($"""
            <div style="font-family:Arial,sans-serif;line-height:1.55;color:#132126;background:#edf7f8;padding:12px 8px">
              <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #c7e3e7;border-radius:10px;overflow:hidden">
                <div style="background:#007f92;padding:9px 12px;text-align:center">
                  <img src="{EmailLogoUrl}" width="72" alt="Zadna" style="display:block;width:72px;max-width:72px;height:auto;border:0;margin:0 auto" />
                </div>
                <div style="padding:18px 20px 18px">
        """);

        if (!string.IsNullOrWhiteSpace(heroUrlEn))
        {
            builder.Append($"""
              <div style="max-width:440px;margin:0 auto 16px;border:1px solid #c7e3e7;border-radius:10px;overflow:hidden;background:#f7fbfc">
                <img src="{heroUrlEn}" width="440" alt="Zadna update" style="display:block;width:100%;max-width:440px;height:auto;border:0;margin:0 auto" />
              </div>
            """);
        }

        if (!string.IsNullOrWhiteSpace(titleEn))
        {
            builder.Append($"""
              <h2 style="margin:0 0 10px;color:#073843;font-size:18px;line-height:1.25">{titleEn}</h2>
            """);
        }

        if (!string.IsNullOrWhiteSpace(bodyEn))
        {
            builder.Append($"""
              <div style="margin:12px 0 0;padding:12px 14px;background:#f7fbfc;border:1px solid #c7e3e7;border-radius:8px">
                <p style="margin:0;color:#132126">{bodyEn}</p>
              </div>
            """);
        }

        if (!string.IsNullOrWhiteSpace(titleAr) || !string.IsNullOrWhiteSpace(bodyAr))
        {
            builder.Append("""
              <hr style="border:none;border-top:1px solid #e2e8f0;margin:20px 0" />
              <div dir="rtl" style="font-family:Tahoma,Arial,sans-serif">
            """);

            if (!string.IsNullOrWhiteSpace(heroUrlAr)
                && !string.Equals(heroUrlAr, heroUrlEn, StringComparison.OrdinalIgnoreCase))
            {
                builder.Append($"""
                <div style="max-width:440px;margin:0 auto 16px;border:1px solid #c7e3e7;border-radius:10px;overflow:hidden;background:#f7fbfc">
                  <img src="{heroUrlAr}" width="440" alt="تحديث من زادنا" style="display:block;width:100%;max-width:440px;height:auto;border:0;margin:0 auto" />
                </div>
                """);
            }

            if (!string.IsNullOrWhiteSpace(titleAr))
            {
                builder.Append($"""
                <h3 style="margin:0 0 8px;color:#073843;font-size:18px;line-height:1.35">{titleAr}</h3>
                """);
            }

            if (!string.IsNullOrWhiteSpace(bodyAr))
            {
                builder.Append($"""
                <div style="margin:12px 0 0;padding:12px 14px;background:#f7fbfc;border:1px solid #c7e3e7;border-radius:8px">
                  <p style="margin:0;color:#132126">{bodyAr}</p>
                </div>
                """);
            }

            builder.Append("</div>");
        }

        if (!string.IsNullOrWhiteSpace(actionUrl))
        {
            builder.Append($"""
              <p style="margin-top:20px">
                <a href="{actionUrl}" style="display:inline-block;background:#007f92;color:#fff;text-decoration:none;padding:10px 16px;border-radius:10px;border-bottom:3px solid #f08010">
                  {actionLabel}
                </a>
              </p>
            """);
        }

        builder.Append("</div></div></div>");
        return builder.ToString();
    }

    private static string RenderTemplate(string template, IReadOnlyDictionary<string, string>? variables)
    {
        if (variables is null || variables.Count == 0 || string.IsNullOrWhiteSpace(template))
        {
            return template;
        }

        var result = template;
        foreach (var item in variables)
        {
            result = result.Replace(item.Key, item.Value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> NormalizeVariables(IReadOnlyDictionary<string, string>? variables)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (variables is null)
        {
            return result;
        }

        foreach (var item in variables)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            var key = item.Key.Trim();
            if (!key.StartsWith("{{", StringComparison.Ordinal))
            {
                key = "{{" + key;
            }

            if (!key.EndsWith("}}", StringComparison.Ordinal))
            {
                key += "}}";
            }

            result[key] = item.Value ?? string.Empty;
        }

        return result;
    }

    private static Dictionary<string, string> BuildVendorTemplateVariables(Vendor vendor, VendorCommunicationMessage message)
    {
        var vendorName = string.IsNullOrWhiteSpace(vendor.BusinessNameEn) ? vendor.BusinessNameAr : vendor.BusinessNameEn;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{{vendor_name}}"] = vendorName,
            ["{{vendor_id}}"] = vendor.Id.ToString(),
            ["{{target_url}}"] = message.TargetUrl
        };

        if (message.TemplateVariables is not null)
        {
            foreach (var item in message.TemplateVariables)
            {
                if (!string.IsNullOrWhiteSpace(item.Key))
                {
                    values[item.Key] = item.Value ?? string.Empty;
                }
            }
        }

        return values;
    }

    private EmailDispatchLog CreateDispatchLog(
        EmailWorkflowRuleDto? rule,
        EmailResolvedRecipientsDto resolved,
        string subject,
        string source,
        EmailSendResult sendResult,
        string? reasonOverride,
        string? eventKey,
        Guid? entityId,
        Guid? vendorId,
        Guid? branchId,
        bool isTestSend)
    {
        return CreateDispatchLog(
            rule,
            rule?.Id,
            rule?.TitleKey ?? "Email dispatch",
            rule?.AudienceType ?? "vendor_network",
            resolved,
            subject,
            source,
            sendResult,
            reasonOverride,
            eventKey,
            entityId,
            vendorId,
            branchId,
            isTestSend);
    }

    private EmailDispatchLog CreateDispatchLog(
        EmailWorkflowRuleDto? rule,
        string? ruleKey,
        string ruleLabel,
        string audienceType,
        EmailResolvedRecipientsDto resolved,
        string subject,
        string source,
        EmailSendResult sendResult,
        string? reasonOverride,
        string? eventKey,
        Guid? entityId,
        Guid? vendorId,
        Guid? branchId,
        bool isTestSend)
    {
        return new EmailDispatchLog(
            ruleKey,
            ruleLabel,
            audienceType,
            source,
            sendResult.Success ? "sent" : "failed",
            subject,
            Serialize(resolved.To),
            Serialize(resolved.Cc),
            Serialize(resolved.Bcc),
            sendResult.Provider,
            sendResult.ProviderMessageId,
            reasonOverride,
            eventKey,
            _currentUserService.UserId,
            entityId,
            vendorId,
            branchId,
            isTestSend);
    }

    private async Task LogAutomationSkipAsync(
        EmailWorkflowRuleDto rule,
        Vendor vendor,
        string? eventKey,
        string reason,
        CancellationToken cancellationToken)
    {
        var log = new EmailDispatchLog(
            rule.Id,
            rule.TitleKey,
            rule.AudienceType,
            "vendor_automation_live",
            "skipped",
            rule.Template.Subject.GetValueOrDefault("en") ?? rule.TitleKey,
            Serialize(Array.Empty<string>()),
            Serialize(Array.Empty<string>()),
            Serialize(Array.Empty<string>()),
            null,
            null,
            reason,
            eventKey,
            _currentUserService.UserId,
            vendor.UserId,
            vendor.Id,
            ParseGuid(rule.EntityScope.BranchId),
            false);

        _context.EmailDispatchLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private Task<EmailDispatchOperationResult> LogSystemEventSkipAsync(
        EmailWorkflowRuleDto rule,
        string eventKey,
        string source,
        EmailSystemEventDispatchRequest request,
        string reason,
        CancellationToken cancellationToken)
    {
        var variables = NormalizeVariables(request.Variables);
        var subject = RenderTemplate(
            rule.Template.Subject.GetValueOrDefault("en") ??
            rule.Template.Subject.GetValueOrDefault("ar") ??
            rule.TitleKey,
            variables);

        return LogSystemEventSkipAsync(
            rule,
            rule.Id,
            rule.TitleKey,
            rule.AudienceType,
            eventKey,
            source,
            request.EntityId,
            request.VendorId,
            request.BranchId,
            string.IsNullOrWhiteSpace(subject) ? rule.TitleKey : subject,
            reason,
            cancellationToken);
    }

    private async Task<EmailDispatchOperationResult> LogSystemEventSkipAsync(
        EmailWorkflowRuleDto? rule,
        string? ruleKey,
        string ruleLabel,
        string audienceType,
        string eventKey,
        string source,
        Guid? entityId,
        Guid? vendorId,
        Guid? branchId,
        string subject,
        string reason,
        CancellationToken cancellationToken)
    {
        var log = new EmailDispatchLog(
            ruleKey,
            ruleLabel,
            audienceType,
            source,
            "skipped",
            subject,
            Serialize(Array.Empty<string>()),
            Serialize(Array.Empty<string>()),
            Serialize(Array.Empty<string>()),
            null,
            null,
            reason,
            eventKey,
            _currentUserService.UserId,
            entityId,
            vendorId,
            branchId,
            false);

        _context.EmailDispatchLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        return new EmailDispatchOperationResult(
            Attempted: false,
            Sent: false,
            Skipped: true,
            Source: source,
            Provider: null,
            ProviderMessageId: null,
            Reason: reason);
    }

    private async Task<EmailDispatchOperationResult> LogVendorLifecycleSkipAsync(
        Vendor vendor,
        string eventKey,
        string reason,
        CancellationToken cancellationToken)
    {
        var log = new EmailDispatchLog(
            null,
            HumanizeEventKey(eventKey),
            "vendor_network",
            "vendor_automation_live",
            "skipped",
            HumanizeEventKey(eventKey),
            Serialize(Array.Empty<string>()),
            Serialize(Array.Empty<string>()),
            Serialize(Array.Empty<string>()),
            null,
            null,
            reason,
            eventKey,
            _currentUserService.UserId,
            vendor.UserId,
            vendor.Id,
            null,
            false);

        _context.EmailDispatchLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        return new EmailDispatchOperationResult(false, false, true, "vendor_automation_live", null, null, reason);
    }

    private async Task<bool> HasDuplicateDispatchAsync(
        string eventKey,
        Guid? entityId,
        Guid? vendorId,
        Guid? branchId,
        DateTime? duplicateWindowStartUtc,
        DateTime? duplicateWindowEndUtc,
        CancellationToken cancellationToken)
    {
        var query = _context.EmailDispatchLogs
            .AsNoTracking()
            .Where(x => !x.IsTestSend && x.EventKey == eventKey);

        if (entityId.HasValue)
        {
            query = query.Where(x => x.EntityId == entityId.Value);
        }

        if (vendorId.HasValue)
        {
            query = query.Where(x => x.VendorId == vendorId.Value);
        }

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (duplicateWindowStartUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= duplicateWindowStartUtc.Value);
        }

        if (duplicateWindowEndUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc < duplicateWindowEndUtc.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    private static List<string> CombineRecipients(EmailResolvedRecipientsDto resolved) =>
        resolved.To.Concat(resolved.Cc).Concat(resolved.Bcc).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static string ResolveVendorEmail(Vendor vendor) =>
        !string.IsNullOrWhiteSpace(vendor.OwnerEmail)
            ? vendor.OwnerEmail!
            : vendor.ContactEmail;

    private static EmailWorkflowRuleDto ApplyVendorRuntimeScope(EmailWorkflowRuleDto rule, Vendor vendor)
    {
        return rule with
        {
            EntityScope = rule.EntityScope with
            {
                VendorId = rule.EntityScope.VendorId ?? vendor.Id.ToString()
            }
        };
    }

    private static string HumanizeEventKey(string eventKey)
    {
        return string.Join(' ', eventKey
            .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static string? NormalizeRegion(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;

    private static EmailSenderProfileDto MapSenderProfile(EmailSenderProfileConfig config) =>
        new(
            config.ProfileKey,
            config.Name,
            config.Address,
            config.ReplyTo,
            config.DescriptionKey,
            config.Locale,
            config.IsDefault,
            config.Status,
            config.IsReadOnly);

    private static EmailWorkflowRuleDto MapRule(EmailWorkflowRuleConfig config, EmailDispatchSummaryDto? lastDispatch)
    {
        var personaTargets = Deserialize<List<string>>(config.PersonaTargetsJson) ?? [];
        var entityScope = Deserialize<EmailEntityScopeDto>(config.EntityScopeJson) ?? new EmailEntityScopeDto(null, null, null);
        var recipientTargets = Deserialize<EmailRecipientTargetSelectionDto>(config.RecipientTargetsJson)
                               ?? new EmailRecipientTargetSelectionDto([], [], []);
        var route = Deserialize<EmailRecipientRouteDto>(config.RouteJson)
                    ?? new EmailRecipientRouteDto([], [], [], [], [], [], string.Empty, string.Empty);
        var template = Deserialize<EmailTemplatePreviewDto>(config.TemplateJson)
                       ?? new EmailTemplatePreviewDto(new Dictionary<string, string>(), new Dictionary<string, string>(), []);

        return new EmailWorkflowRuleDto(
            config.RuleKey,
            config.TitleKey,
            config.SubtitleKey,
            config.CategoryKey,
            config.CadenceLabelKey,
            config.TriggerNotesKey,
            config.Enabled,
            config.SenderProfileKey,
            config.AudienceType,
            config.PanelScope,
            personaTargets,
            entityScope,
            config.BranchScopeMode,
            recipientTargets,
            route,
            template,
            config.AutomationState,
            config.EventKey,
            lastDispatch);
    }

    private static EmailDispatchLogDto MapDispatchLog(EmailDispatchLog log) =>
        new(
            log.Id,
            log.RuleKey,
            log.RuleLabel,
            log.AudienceType,
            log.Source,
            log.Status,
            log.Subject,
            Deserialize<List<string>>(log.ToRecipientsJson) ?? [],
            Deserialize<List<string>>(log.CcRecipientsJson) ?? [],
            Deserialize<List<string>>(log.BccRecipientsJson) ?? [],
            log.Provider,
            log.ProviderMessageId,
            log.FailureReason,
            log.EventKey,
            log.IsTestSend,
            log.CreatedAtUtc);

    private static EmailWorkflowRuleDto NormalizeRule(string ruleId, EmailWorkflowRuleDto draft)
    {
        var normalizedId = string.IsNullOrWhiteSpace(draft.Id) ? ruleId.Trim() : draft.Id.Trim();
        if (!string.Equals(normalizedId, ruleId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("EMAIL_CENTER_RULE_ID_MISMATCH", "The rule id in the route does not match the body.");
        }

        return draft with
        {
            Id = normalizedId,
            TitleKey = draft.TitleKey.Trim(),
            SubtitleKey = draft.SubtitleKey.Trim(),
            CategoryKey = draft.CategoryKey.Trim(),
            CadenceLabelKey = draft.CadenceLabelKey.Trim(),
            TriggerNotesKey = draft.TriggerNotesKey.Trim(),
            SenderProfileId = draft.SenderProfileId.Trim(),
            AudienceType = draft.AudienceType.Trim(),
            PanelScope = draft.PanelScope.Trim(),
            PersonaTargets = draft.PersonaTargets
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            EntityScope = new EmailEntityScopeDto(
                NormalizeOptional(draft.EntityScope.EntityId),
                NormalizeOptional(draft.EntityScope.VendorId),
                NormalizeOptional(draft.EntityScope.BranchId)),
            BranchScopeMode = draft.BranchScopeMode.Trim(),
            RecipientTargets = new EmailRecipientTargetSelectionDto(
                NormalizeEmails(draft.RecipientTargets.To, lowerCase: false),
                NormalizeEmails(draft.RecipientTargets.Cc, lowerCase: false),
                NormalizeEmails(draft.RecipientTargets.Bcc, lowerCase: false)),
            Route = new EmailRecipientRouteDto(
                NormalizeEmails(draft.Route.StaticTo),
                NormalizeEmails(draft.Route.StaticCc),
                NormalizeEmails(draft.Route.StaticBcc),
                NormalizeEmails(draft.Route.FallbackTo),
                NormalizeEmails(draft.Route.FallbackCc),
                NormalizeEmails(draft.Route.FallbackBcc),
                draft.Route.Owner.Trim(),
                draft.Route.Escalation.Trim()),
            Template = new EmailTemplatePreviewDto(
                NormalizeTemplateMap(draft.Template.Subject),
                NormalizeTemplateMap(draft.Template.Body),
                draft.Template.Variables
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                NormalizeOptional(draft.Template.HeroImageUrl),
                NormalizeOptional(draft.Template.CtaLabel),
                NormalizeOptional(draft.Template.HeroImageUrlAr),
                NormalizeOptional(draft.Template.HeroImageUrlEn)),
            AutomationState = string.IsNullOrWhiteSpace(draft.AutomationState)
                ? "manual_only"
                : draft.AutomationState.Trim().ToLowerInvariant(),
            EventKey = NormalizeOptional(draft.EventKey)
        };
    }

    private static Dictionary<string, string> NormalizeTemplateMap(Dictionary<string, string> source)
    {
        return source
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(
                item => item.Key.Trim().ToLowerInvariant(),
                item => item.Value?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> NormalizeEmails(IEnumerable<string> values, bool lowerCase = true)
    {
        return values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => lowerCase ? x.Trim().ToLowerInvariant() : x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private sealed record DirectoryRecipientRecord(
        Guid UserId,
        string Email,
        string PersonaType,
        string AudienceType,
        string PanelScope,
        Guid? EntityId,
        Guid? VendorId,
        Guid? BranchId,
        string? Region);
}

internal static class EmailCenterDefaults
{
    private const string OrderUpdateHeroImageUrlAr = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2025,%202026,%2004_26_04%20PM.png";
    private const string OrderUpdateHeroImageUrlEn = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2025,%202026,%2004_27_36%20PM.png";
    private const string VendorNewOrderHeroImageUrlAr = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2025,%202026,%2005_40_44%20PM.png";
    private const string VendorNewOrderHeroImageUrlEn = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2025,%202026,%2004_17_35%20PM.png";
    private const string VendorApprovedHeroImageUrlAr = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2025,%202026,%2004_35_00%20PM.png";
    private const string VendorApprovedHeroImageUrlEn = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2025,%202026,%2004_45_14%20PM.png";
    private const string DriverVerificationHeroImageUrlAr = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2025,%202026,%2004_49_56%20PM.png";
    private const string DriverVerificationHeroImageUrlEn = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2025,%202026,%2004_49_36%20PM.png";
    private const string SupportCaseHeroImageUrlAr = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2026,%202026,%2001_04_55%20PM.png";
    private const string SupportCaseHeroImageUrlEn = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2026,%202026,%2001_04_45%20PM.png";
    private const string VendorWeeklySummaryHeroImageUrlAr = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2026,%202026,%2002_07_18%20PM.png";
    private const string VendorWeeklySummaryHeroImageUrlEn = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/ChatGPT%20Image%20May%2026,%202026,%2002_10_42%20PM.png";

    public static IReadOnlyList<EmailSenderProfileConfig> BuildSenderProfiles() =>
    [
        new EmailSenderProfileConfig(
            "ops-primary",
            "Zadna Support",
            "support@zadna0.com",
            "support@zadna0.com",
            "EMAIL_CENTER.PROFILES.OPS_PRIMARY",
            "bilingual",
            true,
            "primary"),
        new EmailSenderProfileConfig(
            "vendor-network",
            "Zadna Hello",
            "hello@zadna0.com",
            "contact@zadna0.com",
            "EMAIL_CENTER.PROFILES.VENDOR_NETWORK",
            "bilingual",
            false,
            "secondary"),
        new EmailSenderProfileConfig(
            "finance-digest",
            "Zadna Info",
            "info@zadna0.com",
            "contact@zadna0.com",
            "EMAIL_CENTER.PROFILES.FINANCE_DIGEST",
            "english",
            false,
            "backup")
    ];

    public static IReadOnlyList<EmailWorkflowRuleConfig> BuildWorkflowRules()
    {
        var rules = new List<EmailWorkflowRuleConfig>
        {
            BuildRule(
                "customer-order-confirmed",
                "Order confirmed",
                "Receipt and order confirmation for customers.",
                "Customer Orders",
                "Instant",
                "Sends only when an order is confirmed after payment/COD placement.",
                true,
                "ops-primary",
                "customers",
                "customer_app",
                ["customer"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto([], [], []),
                new EmailRecipientRouteDto([], [], [], ["support@zadna0.com"], [], [], "Customer Experience", "Orders Desk"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Your Zadna order {{order_number}} is confirmed", ["ar"] = "تم تأكيد طلبك {{order_number}} من زادنا" },
                    new Dictionary<string, string> { ["en"] = "Hi {{customer_name}}, your order from {{vendor_name}} is confirmed. Total: {{order_total}} {{currency}}.", ["ar"] = "أهلا {{customer_name}}، تم تأكيد طلبك من {{vendor_name}}. الإجمالي: {{order_total}} {{currency}}." },
                    ["{{customer_name}}", "{{order_number}}", "{{vendor_name}}", "{{order_total}}", "{{currency}}"],
                    OrderUpdateHeroImageUrlEn,
                    "Track order",
                    OrderUpdateHeroImageUrlAr,
                    OrderUpdateHeroImageUrlEn),
                "live",
                EmailEventKeys.CustomerOrderConfirmed),
            BuildRule(
                "customer-order-out-for-delivery",
                "Order out for delivery",
                "Customer update when an order is on the way.",
                "Customer Orders",
                "Instant",
                "Sends only when an order reaches OnTheWay.",
                true,
                "ops-primary",
                "customers",
                "customer_app",
                ["customer"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto([], [], []),
                new EmailRecipientRouteDto([], [], [], ["support@zadna0.com"], [], [], "Customer Experience", "Delivery Desk"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Your order {{order_number}} is on the way", ["ar"] = "طلبك {{order_number}} خرج للتوصيل" },
                    new Dictionary<string, string> { ["en"] = "Your order from {{vendor_name}} is now with the driver and heading to you.", ["ar"] = "طلبك من {{vendor_name}} أصبح مع المندوب وفي الطريق إليك." },
                    ["{{order_number}}", "{{vendor_name}}"],
                    OrderUpdateHeroImageUrlEn,
                    "Track delivery",
                    OrderUpdateHeroImageUrlAr,
                    OrderUpdateHeroImageUrlEn),
                "live",
                EmailEventKeys.CustomerOrderOutForDelivery),
            BuildRule(
                "customer-order-important-update",
                "Important order update",
                "Customer email for cancellation, refund, delivery failure, or payment failure.",
                "Customer Orders",
                "Instant",
                "Sends only for major order/payment changes.",
                true,
                "ops-primary",
                "customers",
                "customer_app",
                ["customer"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto([], [], []),
                new EmailRecipientRouteDto([], [], [], ["support@zadna0.com"], [], [], "Customer Experience", "Support Desk"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Important update for order {{order_number}}", ["ar"] = "تحديث مهم بخصوص طلبك {{order_number}}" },
                    new Dictionary<string, string> { ["en"] = "{{update_message}}", ["ar"] = "{{update_message}}" },
                    ["{{order_number}}", "{{update_message}}"],
                    OrderUpdateHeroImageUrlEn,
                    "Open order",
                    OrderUpdateHeroImageUrlAr,
                    OrderUpdateHeroImageUrlEn),
                "live",
                EmailEventKeys.CustomerOrderImportantUpdate),
            BuildRule(
                "vendor-order-action-required",
                "New order needs action",
                "Vendor email only when an order is waiting for acceptance/preparation.",
                "Vendor Orders",
                "Instant",
                "Sends only for PendingVendorAcceptance orders.",
                true,
                "vendor-network",
                "vendor_network",
                "vendor_panel",
                ["vendor_owner"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto([], [], []),
                new EmailRecipientRouteDto([], [], [], ["contact@zadna0.com"], [], [], "Vendor Operations", "Marketplace Operations"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Order {{order_number}} needs your action", ["ar"] = "طلب {{order_number}} يحتاج إجراء منك" },
                    new Dictionary<string, string> { ["en"] = "{{vendor_name}}, a new order is waiting for your confirmation. Total: {{order_total}} {{currency}}.", ["ar"] = "{{vendor_name}}، يوجد طلب جديد في انتظار تأكيدك. الإجمالي: {{order_total}} {{currency}}." },
                    ["{{vendor_name}}", "{{order_number}}", "{{order_total}}", "{{currency}}"],
                    VendorNewOrderHeroImageUrlEn,
                    "Open order",
                    VendorNewOrderHeroImageUrlAr,
                    VendorNewOrderHeroImageUrlEn),
                "live",
                EmailEventKeys.VendorOrderActionRequired),
            BuildRule(
                "vendor-weekly-summary",
                "Weekly vendor summary",
                "Weekly digest of vendor sales, orders, cancellations, and top products.",
                "Finance",
                "Weekly",
                "Sends Mondays at 09:00 Africa/Cairo when the vendor had order activity last week.",
                true,
                "finance-digest",
                "vendor_network",
                "vendor_panel",
                ["vendor_owner"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto([], [], []),
                new EmailRecipientRouteDto([], [], [], ["info@zadna0.com"], [], [], "Finance Operations", "Vendor Success"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Your Zadna weekly summary: {{week_label}}", ["ar"] = "ملخص زادنا الأسبوعي: {{week_label}}" },
                    new Dictionary<string, string> { ["en"] = "{{summary_body}}", ["ar"] = "{{summary_body}}" },
                    ["{{vendor_name}}", "{{week_label}}", "{{summary_body}}"],
                    VendorWeeklySummaryHeroImageUrlEn,
                    "Open dashboard",
                    VendorWeeklySummaryHeroImageUrlAr,
                    VendorWeeklySummaryHeroImageUrlEn),
                "live",
                EmailEventKeys.VendorWeeklySummary),
            BuildRule(
                "super-admin-access-invite",
                "EMAIL_CENTER.EVENTS.SUPER_ADMIN_ACCESS_INVITE.TITLE",
                "EMAIL_CENTER.EVENTS.SUPER_ADMIN_ACCESS_INVITE.SUBTITLE",
                "EMAIL_CENTER.CATEGORIES.ACCESS",
                "EMAIL_CENTER.CADENCE.INSTANT",
                "EMAIL_CENTER.NOTES.SUPER_ADMIN_ACCESS_INVITE",
                true,
                "ops-primary",
                "super_admin",
                "super_admin_panel",
                ["super_admin_manager", "super_admin_staff"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto(["primary_account_email"], ["assigned_super_admin_manager"], []),
                new EmailRecipientRouteDto(["support@zadna0.com"], [], [], ["support@zadna0.com"], ["contact@zadna0.com"], [], "Access Control Desk", "Security Governance"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Your Zadana access is ready", ["ar"] = "تم تجهيز وصولك في زادانا" },
                    new Dictionary<string, string> { ["en"] = "Your super admin access invitation is ready. Complete onboarding before the expiry date.", ["ar"] = "دعوة الوصول الخاصة بك جاهزة. يرجى إكمال التفعيل قبل تاريخ الانتهاء." },
                    ["{{full_name}}", "{{expiry_date}}", "{{invite_link}}"]),
                "manual_only"),
            BuildRule(
                "vendor-branch-invite",
                "EMAIL_CENTER.EVENTS.VENDOR_BRANCH_INVITE.TITLE",
                "EMAIL_CENTER.EVENTS.VENDOR_BRANCH_INVITE.SUBTITLE",
                "EMAIL_CENTER.CATEGORIES.VENDOR_NETWORK",
                "EMAIL_CENTER.CADENCE.INSTANT",
                "EMAIL_CENTER.NOTES.VENDOR_BRANCH_INVITE",
                true,
                "vendor-network",
                "vendor_network",
                "vendor_panel",
                ["vendor_owner", "vendor_branch_manager", "vendor_branch_employee"],
                new EmailEntityScopeDto(null, null, null),
                "specific_branch",
                new EmailRecipientTargetSelectionDto(["branch_manager", "vendor_owner"], ["assigned_super_admin_manager"], []),
                new EmailRecipientRouteDto([], ["hello@zadna0.com"], [], ["contact@zadna0.com"], ["support@zadna0.com"], [], "Vendor Success Hub", "Marketplace Operations"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Branch access onboarding", ["ar"] = "تهيئة وصول الفرع" },
                    new Dictionary<string, string> { ["en"] = "Branch team access has been prepared. Review role scope and complete activation.", ["ar"] = "تم تجهيز وصول فريق الفرع. يرجى مراجعة نطاق الدور واستكمال التفعيل." },
                    ["{{branch_name}}", "{{vendor_name}}", "{{invite_link}}"]),
                "manual_only"),
            BuildRule(
                "branch-password-reset",
                "EMAIL_CENTER.EVENTS.BRANCH_PASSWORD_RESET.TITLE",
                "EMAIL_CENTER.EVENTS.BRANCH_PASSWORD_RESET.SUBTITLE",
                "EMAIL_CENTER.CATEGORIES.VENDOR_NETWORK",
                "EMAIL_CENTER.CADENCE.INSTANT",
                "EMAIL_CENTER.NOTES.BRANCH_PASSWORD_RESET",
                true,
                "vendor-network",
                "vendor_network",
                "vendor_panel",
                ["vendor_branch_manager", "vendor_branch_employee"],
                new EmailEntityScopeDto(null, null, null),
                "specific_branch",
                new EmailRecipientTargetSelectionDto(["branch_manager", "branch_staff"], ["vendor_company_manager"], []),
                new EmailRecipientRouteDto([], [], [], ["support@zadna0.com"], ["contact@zadna0.com"], [], "Vendor Identity Support", "Vendor Security Desk"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Reset requested for branch credentials", ["ar"] = "تم طلب إعادة تعيين بيانات الفرع" },
                    new Dictionary<string, string> { ["en"] = "A secure password reset was requested for the branch account.", ["ar"] = "تم طلب إعادة تعيين آمن لبيانات الفرع." },
                    ["{{branch_name}}", "{{reset_link}}", "{{requested_at}}"]),
                "manual_only"),
            BuildRule(
                "vendor-finance-digest",
                "EMAIL_CENTER.EVENTS.VENDOR_FINANCE_DIGEST.TITLE",
                "EMAIL_CENTER.EVENTS.VENDOR_FINANCE_DIGEST.SUBTITLE",
                "EMAIL_CENTER.CATEGORIES.FINANCE",
                "EMAIL_CENTER.CADENCE.DAILY",
                "EMAIL_CENTER.NOTES.VENDOR_FINANCE_DIGEST",
                true,
                "finance-digest",
                "vendor_network",
                "vendor_panel",
                ["vendor_owner", "vendor_finance", "vendor_company_manager"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto(["vendor_finance"], ["vendor_owner", "vendor_company_manager"], ["assigned_super_admin_manager"]),
                new EmailRecipientRouteDto(["info@zadna0.com"], [], [], ["info@zadna0.com"], ["contact@zadna0.com"], [], "Finance Operations", "CFO Office"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Vendor finance digest", ["ar"] = "ملخص مالية التاجر" },
                    new Dictionary<string, string> { ["en"] = "Daily finance digest for the selected vendor scope.", ["ar"] = "ملخص مالي يومي لنطاق التاجر المحدد." },
                    ["{{business_date}}", "{{vendor_name}}"]),
                "manual_only"),
            BuildRule(
                "driver-verification-update",
                "EMAIL_CENTER.EVENTS.DRIVER_VERIFICATION_UPDATE.TITLE",
                "EMAIL_CENTER.EVENTS.DRIVER_VERIFICATION_UPDATE.SUBTITLE",
                "EMAIL_CENTER.CATEGORIES.DRIVERS",
                "EMAIL_CENTER.CADENCE.INSTANT",
                "EMAIL_CENTER.NOTES.DRIVER_VERIFICATION_UPDATE",
                true,
                "ops-primary",
                "drivers",
                "driver_app",
                ["driver"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto(["driver_account"], [], []),
                new EmailRecipientRouteDto([], [], [], [], [], [], "Driver Operations", "Driver Compliance"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Driver verification update", ["ar"] = "تحديث حالة توثيق المندوب" },
                    new Dictionary<string, string> { ["en"] = "Your driver verification status has changed. Open the driver app for details.", ["ar"] = "تم تحديث حالة توثيق المندوب. افتح التطبيق للاطلاع على التفاصيل." },
                    ["{{driver_name}}", "{{status}}", "{{driver_note}}"],
                    DriverVerificationHeroImageUrlEn,
                    "Open driver app",
                    DriverVerificationHeroImageUrlAr,
                    DriverVerificationHeroImageUrlEn),
                "live",
                EmailEventKeys.DriverVerificationUpdate),
            BuildRule(
                "driver-payout-alert",
                "EMAIL_CENTER.EVENTS.DRIVER_PAYOUT_ALERT.TITLE",
                "EMAIL_CENTER.EVENTS.DRIVER_PAYOUT_ALERT.SUBTITLE",
                "EMAIL_CENTER.CATEGORIES.FINANCE",
                "EMAIL_CENTER.CADENCE.INSTANT",
                "EMAIL_CENTER.NOTES.DRIVER_PAYOUT_ALERT",
                true,
                "finance-digest",
                "drivers",
                "driver_app",
                ["driver"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto(["driver_account"], [], []),
                new EmailRecipientRouteDto([], [], [], ["info@zadna0.com"], [], [], "Driver Finance Desk", "Driver Finance Lead"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Driver payout alert", ["ar"] = "تنبيه دفعة المندوب" },
                    new Dictionary<string, string> { ["en"] = "A payout-related update is available for your driver account.", ["ar"] = "هناك تحديث متعلق بالدفعات على حساب المندوب الخاص بك." },
                    ["{{amount}}", "{{payout_reference}}"]),
                "manual_only"),
            BuildRule(
                "customer-support-escalation",
                "EMAIL_CENTER.EVENTS.CUSTOMER_SUPPORT_ESCALATION.TITLE",
                "EMAIL_CENTER.EVENTS.CUSTOMER_SUPPORT_ESCALATION.SUBTITLE",
                "EMAIL_CENTER.CATEGORIES.SUPPORT",
                "EMAIL_CENTER.CADENCE.INSTANT",
                "EMAIL_CENTER.NOTES.CUSTOMER_SUPPORT_ESCALATION",
                true,
                "ops-primary",
                "customers",
                "customer_app",
                ["customer"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto(["customer_account"], [], []),
                new EmailRecipientRouteDto([], [], [], ["support@zadna0.com"], [], [], "Customer Experience", "Support Escalation Desk"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Update on support case {{case_number}}", ["ar"] = "تحديث على طلب الدعم {{case_number}}" },
                    new Dictionary<string, string>
                    {
                        ["en"] = "We have an update on your {{case_type}} for order {{order_number}}. Current status: {{status}}. {{support_message}} Next step: {{next_step}}",
                        ["ar"] = "لدينا تحديث على {{case_type}} الخاص بطلبك {{order_number}}. الحالة الحالية: {{status}}. {{support_message}} الخطوة التالية: {{next_step}}"
                    },
                    ["{{case_number}}", "{{case_type}}", "{{order_number}}", "{{status}}", "{{support_message}}", "{{next_step}}"],
                    SupportCaseHeroImageUrlEn,
                    "Open support case",
                    SupportCaseHeroImageUrlAr,
                    SupportCaseHeroImageUrlEn),
                "manual_only"),
            BuildRule(
                "customer-account-recovery",
                "EMAIL_CENTER.EVENTS.CUSTOMER_ACCOUNT_RECOVERY.TITLE",
                "EMAIL_CENTER.EVENTS.CUSTOMER_ACCOUNT_RECOVERY.SUBTITLE",
                "EMAIL_CENTER.CATEGORIES.ACCESS",
                "EMAIL_CENTER.CADENCE.INSTANT",
                "EMAIL_CENTER.NOTES.CUSTOMER_ACCOUNT_RECOVERY",
                true,
                "ops-primary",
                "customers",
                "customer_app",
                ["customer"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto(["customer_account"], [], []),
                new EmailRecipientRouteDto([], [], [], ["support@zadna0.com"], [], [], "Identity Support", "Customer Security Desk"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string> { ["en"] = "Customer account recovery", ["ar"] = "استعادة حساب العميل" },
                    new Dictionary<string, string> { ["en"] = "A recovery action was requested for your customer account.", ["ar"] = "تم طلب إجراء استعادة لحساب العميل الخاص بك." },
                    ["{{reset_link}}", "{{requested_at}}"]),
                "manual_only")
        };

        foreach (var liveRule in BuildVendorLiveRules())
        {
            rules.Add(liveRule);
        }

        return rules;
    }

    private static IEnumerable<EmailWorkflowRuleConfig> BuildVendorLiveRules()
    {
        var eventKeys = new[]
        {
            EmailEventKeys.VendorApproved,
            EmailEventKeys.VendorPasswordReset
        };

        foreach (var eventKey in eventKeys)
        {
            var isVendorApproved = string.Equals(eventKey, EmailEventKeys.VendorApproved, StringComparison.OrdinalIgnoreCase);
            var subject = isVendorApproved
                ? new Dictionary<string, string>
                {
                    ["en"] = "Your Zadna vendor account is approved",
                    ["ar"] = "تم تفعيل حساب التاجر في زادنا"
                }
                : new Dictionary<string, string>
                {
                    ["en"] = Humanize(eventKey),
                    ["ar"] = Humanize(eventKey)
                };
            var body = isVendorApproved
                ? new Dictionary<string, string>
                {
                    ["en"] = "{{vendor_name}}, your vendor account has been approved. You can now open your workspace and manage orders.",
                    ["ar"] = "{{vendor_name}}، تم اعتماد حساب التاجر الخاص بك. يمكنك الآن فتح لوحة التحكم وإدارة الطلبات."
                }
                : new Dictionary<string, string>
                {
                    ["en"] = "This is an automated vendor account update from Zadana. Open your workspace for details.",
                    ["ar"] = "هذا تحديث آلي على حساب التاجر من زادنا. افتح لوحة التاجر للاطلاع على التفاصيل."
                };

            yield return BuildRule(
                $"live-{eventKey}",
                Humanize(eventKey),
                $"Live automation for {Humanize(eventKey)}",
                "Vendor Lifecycle",
                "Instant",
                $"Automatically dispatches when the vendor event `{eventKey}` is triggered.",
                true,
                "vendor-network",
                "vendor_network",
                "vendor_panel",
                ["vendor_owner"],
                new EmailEntityScopeDto(null, null, null),
                "all_branches",
                new EmailRecipientTargetSelectionDto(["vendor_owner"], [], []),
                new EmailRecipientRouteDto([], [], [], ["contact@zadna0.com"], [], [], "Vendor Operations", "Marketplace Operations"),
                new EmailTemplatePreviewDto(
                    new Dictionary<string, string>
                    {
                        ["en"] = subject["en"],
                        ["ar"] = subject["ar"]
                    },
                    new Dictionary<string, string>
                    {
                        ["en"] = body["en"],
                        ["ar"] = body["ar"]
                    },
                    ["{{vendor_name}}", "{{target_url}}"],
                    isVendorApproved ? VendorApprovedHeroImageUrlEn : null,
                    isVendorApproved ? "Open dashboard" : "Open workspace",
                    isVendorApproved ? VendorApprovedHeroImageUrlAr : null,
                    isVendorApproved ? VendorApprovedHeroImageUrlEn : null),
                "live",
                eventKey);
        }
    }

    private static EmailWorkflowRuleConfig BuildRule(
        string id,
        string titleKey,
        string subtitleKey,
        string categoryKey,
        string cadenceLabelKey,
        string triggerNotesKey,
        bool enabled,
        string senderProfileId,
        string audienceType,
        string panelScope,
        IEnumerable<string> personaTargets,
        EmailEntityScopeDto entityScope,
        string branchScopeMode,
        EmailRecipientTargetSelectionDto recipientTargets,
        EmailRecipientRouteDto route,
        EmailTemplatePreviewDto template,
        string automationState,
        string? eventKey = null)
    {
        return new EmailWorkflowRuleConfig(
            id,
            titleKey,
            subtitleKey,
            categoryKey,
            cadenceLabelKey,
            triggerNotesKey,
            enabled,
            senderProfileId,
            audienceType,
            panelScope,
            JsonSerializer.Serialize(personaTargets.ToList(), JsonSerializerOptions.Web),
            JsonSerializer.Serialize(entityScope, JsonSerializerOptions.Web),
            branchScopeMode,
            JsonSerializer.Serialize(recipientTargets, JsonSerializerOptions.Web),
            JsonSerializer.Serialize(route, JsonSerializerOptions.Web),
            JsonSerializer.Serialize(template, JsonSerializerOptions.Web),
            automationState,
            eventKey);
    }

    private static string Humanize(string value) =>
        string.Join(' ', value.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
