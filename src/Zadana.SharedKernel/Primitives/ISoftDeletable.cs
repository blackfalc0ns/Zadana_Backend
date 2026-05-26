namespace Zadana.SharedKernel.Primitives;

/// <summary>
/// Marks an entity as soft-deletable. Instead of physically removing the row,
/// the entity is flagged with <see cref="IsDeleted"/> and hidden via a global
/// query filter in EF Core.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTime? DeletedAtUtc { get; }

    void SoftDelete();
    void Restore();
}
