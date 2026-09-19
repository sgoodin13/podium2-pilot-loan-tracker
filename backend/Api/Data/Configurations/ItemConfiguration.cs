using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

/// <summary>Maps <see cref="Item"/> to <c>items</c>.</summary>
public class ItemConfiguration : EntityBaseConfiguration<Item>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Item> builder)
    {
        builder.Property(i => i.Name).IsRequired();
        builder.Property(i => i.Description).IsRequired(false);
        builder.Property(i => i.AssetTag).IsRequired();
        builder.Property(i => i.ItemCategoryId).IsRequired();

        builder.HasIndex(i => i.AssetTag)
            .IsUnique()
            .HasDatabaseName(DatabaseConstraintNames.ItemAssetTag);

        // FK index, per the DB standard (index every FK).
        builder.HasIndex(i => i.ItemCategoryId)
            .HasDatabaseName("idx_items_item_category_id");

        builder.HasOne(i => i.ItemCategory)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.ItemCategoryId)
            .IsRequired()
            // Restrict: nothing is ever hard-deleted, so a cascade must never be possible.
            .OnDelete(DeleteBehavior.Restrict);
    }
}
