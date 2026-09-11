namespace Mazza.Orders.Domain.Common;

/// <summary>
/// Base class for entities: identity is the <see cref="Id"/>, never the field values.
/// Two <c>Order</c> instances loaded in different DbContexts are the same order
/// if they share an id, which is what makes change tracking and set operations behave.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Entity id cannot be empty.", nameof(id));
        }

        Id = id;
    }

    /// <summary>Parameterless ctor reserved for the EF Core materializer.</summary>
    protected Entity()
    {
    }

    public Guid Id { get; private set; }

    public bool Equals(Entity? other)
    {
        if (other is null)
        {
            return false;
        }

        // Different aggregates never compare equal even if ids collide.
        return GetType() == other.GetType() && Id == other.Id;
    }

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
