using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Infrastructure.Email;

public class HtmlTemplateService : ITemplateService
{
    private readonly ILogger<HtmlTemplateService> _logger;

    public HtmlTemplateService(ILogger<HtmlTemplateService> logger)
    {
        _logger = logger;
    }

    public async Task<string> RenderTemplateAsync(string templateName, Dictionary<string, string> placeholders)
    {
        var isArabic = CultureInfo.CurrentCulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        var languageSuffix = isArabic ? "ar" : "en";
        
        var templateFileName = $"{templateName}.{languageSuffix}.html";
        
        // Find the template file in the base directory
        var basePath = AppContext.BaseDirectory;
        var filePath = Path.Combine(basePath, "Email", "Templates", templateFileName);

        // Fallback to English if the specific Arabic template doesn't exist
        if (!File.Exists(filePath) && isArabic)
        {
            _logger.LogWarning("Template {TemplateName} for culture AR not found at {Path}. Falling back to EN.", templateName, filePath);
            filePath = Path.Combine(basePath, "Email", "Templates", $"{templateName}.en.html");
        }

        if (!File.Exists(filePath))
        {
            _logger.LogError("Email template {TemplateName} not found at {Path}", templateName, filePath);
            throw new FileNotFoundException($"Email template {templateName} not found at {filePath}");
        }

        var templateContent = await File.ReadAllTextAsync(filePath);

        // Replace all placeholders in the format {{Key}} with their respective Values
        foreach (var placeholder in placeholders)
        {
            templateContent = templateContent.Replace("{{" + placeholder.Key + "}}", placeholder.Value);
        }

        return templateContent;
    }
}
