using Mazza.Orders.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mazza.Orders.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the Order aggregate root.
///
/// All of the mapping lives here rather than as attributes on the entity: the domain
/// class stays free of persistence concerns, which is what lets the Domain project
/// have no package references at all.
/// </summary>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.CustomerId)
            .IsRequired();

        // Stored as text ("Pending") rather than as an integer. It costs a few bytes
        // and makes the database readable on its own, which is worth it when someone
        // is debugging with the sqlite CLI at 2am.
        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(order => order.CreatedAt)
            .IsRequired();

        // TotalAmount is derived from the items, so there is nothing to persist.
        // Storing it would create a second source of truth that can drift.
        builder.Ignore(order => order.TotalAmount);

        builder.HasMany(order => order.Items)
            .WithOne()
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // EF writes straight into the backing list instead of through the read-only
        // Items property, so encapsulation survives materialisation.
        builder.Navigation(order => order.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // The list endpoint always sorts by CreatedAt descending; without this index
        // every page would be a full scan plus a sort.
        builder.HasIndex(order => order.CreatedAt)
            .HasDatabaseName("IX_Orders_CreatedAt");

        builder.HasIndex(order => order.CustomerId)
            .HasDatabaseName("IX_Orders_CustomerId");
    }
}
