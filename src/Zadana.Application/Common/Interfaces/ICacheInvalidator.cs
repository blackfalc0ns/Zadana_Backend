namespace Zadana.Application.Common.Interfaces;

public interface ICacheInvalidator
{
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
    Task RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}
