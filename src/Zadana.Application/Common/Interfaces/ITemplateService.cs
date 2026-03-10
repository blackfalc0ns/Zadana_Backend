using System.Collections.Generic;
using System.Threading.Tasks;

namespace Zadana.Application.Common.Interfaces;

public interface ITemplateService
{
    /// <summary>
    /// Renders an HTML template with the given placeholders.
    /// The service should automatically resolve the correct template based on the current culture (ar/en).
    /// </summary>
    Task<string> RenderTemplateAsync(string templateName, Dictionary<string, string> placeholders);
}
