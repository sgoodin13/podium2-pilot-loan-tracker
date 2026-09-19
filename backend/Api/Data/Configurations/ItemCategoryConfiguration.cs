using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

/// <summary>Maps <see cref="ItemCategory"/> to <c>item_categories</c>.</summary>
public class ItemCategoryConfiguration : EntityBaseConfiguration<ItemCategory>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ItemCategory> builder)
    {
        // No HasMaxLength: columns stay unbounded `text` by ruling. Length limits are
        // enforced deliberately at the DTO layer, not in the schema.
        builder.Property(c => c.Name).IsRequired();
        builder.Property(c => c.Description).IsRequired(false);

        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasDatabaseName(DatabaseConstraintNames.ItemCategoryName);
    }
}
