namespace Zadana.SharedKernel.Primitives;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
