using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

/// <summary>
/// Applies the Gate 1 rulings that hold for every table — uuid PK with a
/// <c>gen_random_uuid()</c> database default, the <c>is_active</c> soft-delete flag,
/// and the four audit columns — so no individual configuration restates them.
/// </summary>
/// <remarks>
/// Column names are NOT hand-written anywhere: <c>UseSnakeCaseNamingConvention()</c>
/// (EFCore.NamingConventions) derives them from the PascalCase property names.
/// <para>
/// <c>IsActive</c>, <c>CreatedAt</c>, <c>CheckedOutAt</c> and <c>IsTerminal</c> declare a
/// database default (so hand-written SQL and seed scripts behave) but are marked
/// <c>ValueGeneratedNever()</c> so EF Core always writes the value the application set.
/// Without that, EF treats a CLR-default value (e.g. <c>IsActive = false</c>) as "let the
/// database decide" and would silently insert <c>true</c>.
/// </para>
/// </remarks>
public abstract class EntityBaseConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : EntityBase
{
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValueSql("true")
            .ValueGeneratedNever();

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()")
            .ValueGeneratedNever();

        builder.Property(e => e.CreatedBy)
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .IsRequired(false);

        builder.Property(e => e.ModifiedBy)
            .IsRequired(false);

        ConfigureEntity(builder);
    }

    /// <summary>Per-entity columns, relationships and indexes.</summary>
    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}
