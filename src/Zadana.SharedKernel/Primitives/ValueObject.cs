namespace Zadana.SharedKernel.Primitives;

public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetAtomicValues();

    public bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
            return false;

        return GetAtomicValues().SequenceEqual(other.GetAtomicValues());
    }

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        return obj is ValueObject valueObject && Equals(valueObject);
    }

    public override int GetHashCode()
    {
        return GetAtomicValues()
            .Aggregate(default(int), (current, obj) =>
                HashCode.Combine(current, obj?.GetHashCode() ?? 0));
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
