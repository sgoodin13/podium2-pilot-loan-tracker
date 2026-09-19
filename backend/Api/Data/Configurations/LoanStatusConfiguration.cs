using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

/// <summary>Maps <see cref="LoanStatus"/> to <c>loan_statuses</c>.</summary>
public class LoanStatusConfiguration : EntityBaseConfiguration<LoanStatus>
{
    protected override void ConfigureEntity(EntityTypeBuilder<LoanStatus> builder)
    {
        builder.Property(s => s.Name).IsRequired();
        builder.Property(s => s.Description).IsRequired(false);

        builder.Property(s => s.IsTerminal)
            .IsRequired()
            .HasDefaultValueSql("false")
            .ValueGeneratedNever();

        builder.HasIndex(s => s.Name)
            .IsUnique()
            .HasDatabaseName(DatabaseConstraintNames.LoanStatusName);
    }
}
