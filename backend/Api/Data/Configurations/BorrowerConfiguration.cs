using Api.Data.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

/// <summary>Maps <see cref="Borrower"/> to <c>borrowers</c>.</summary>
public class BorrowerConfiguration : EntityBaseConfiguration<Borrower>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Borrower> builder)
    {
        builder.Property(b => b.Name).IsRequired();
        builder.Property(b => b.ContactEmail).IsRequired(false);
        builder.Property(b => b.ContactPhone).IsRequired(false);
        builder.Property(b => b.Department).IsRequired(false);
    }
}
