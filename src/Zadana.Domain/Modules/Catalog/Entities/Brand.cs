using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class Brand : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string? LogoUrl { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation
    public ICollection<MasterProduct> MasterProducts { get; private set; } = [];

    private Brand() { }

    public Brand(string name, string? logoUrl = null)
    {
        Name = name.Trim();
        LogoUrl = logoUrl?.Trim();
        IsActive = true;
    }

    public void Update(string name, string? logoUrl)
    {
        Name = name.Trim();
        LogoUrl = logoUrl?.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
