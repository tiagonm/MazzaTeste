using Mazza.Orders.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mazza.Orders.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the OrderItem child entity.
/// </summary>
internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.ProductName)
            .HasMaxLength(OrderItem.MaxProductNameLength)
            .IsRequired();

        builder.Property(item => item.Quantity)
            .IsRequired();

        // Explicit precision rather than SQLite's default. The scale of 2 is the same
        // limit the CreateOrder validator enforces, so a value that passes validation
        // is guaranteed to survive the round trip unchanged.
        builder.Property(item => item.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        // Derived from Quantity and UnitPrice - computed on read, never stored.
        builder.Ignore(item => item.LineTotal);

        builder.HasIndex(item => item.OrderId)
            .HasDatabaseName("IX_OrderItems_OrderId");
    }
}
