using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

/// <summary>Maps <see cref="Loan"/> to <c>loans</c>.</summary>
public class LoanConfiguration : EntityBaseConfiguration<Loan>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Loan> builder)
    {
        builder.Ignore(l => l.IsOpen);

        builder.Property(l => l.ItemId).IsRequired();
        builder.Property(l => l.BorrowerId).IsRequired();
        builder.Property(l => l.LoanStatusId).IsRequired();

        builder.Property(l => l.CheckedOutAt)
            .IsRequired()
            .HasDefaultValueSql("now()")
            .ValueGeneratedNever();

        // Null means the loan is OPEN.
        builder.Property(l => l.ReturnedAt).IsRequired(false);

        // ---------------------------------------------------------------------
        // THE constraint this pilot exists to prove (BR-1), enforced in the
        // database rather than in application code: a filtered unique index that
        // makes a second open loan on the same item impossible, even under a race.
        //
        //   CREATE UNIQUE INDEX ux_loans_item_open
        //     ON loans (item_id) WHERE returned_at IS NULL;
        //
        // NOTE the two-argument HasIndex overload. Two single-argument HasIndex calls on
        // the same property do NOT create two indexes — EF returns the same index builder
        // and the second call silently renames the first, which would erase this filter.
        // The explicit model name keeps the filtered unique index and the plain FK index
        // on item_id as two distinct indexes.
        // ---------------------------------------------------------------------
        builder.HasIndex(l => l.ItemId, DatabaseConstraintNames.OpenLoanPerItem)
            .HasFilter("returned_at IS NULL")
            .IsUnique()
            .HasDatabaseName(DatabaseConstraintNames.OpenLoanPerItem);

        // FK indexes, per the DB standard (index every FK).
        builder.HasIndex(l => l.ItemId, "idx_loans_item_id")
            .HasDatabaseName("idx_loans_item_id");

        builder.HasIndex(l => l.BorrowerId)
            .HasDatabaseName("idx_loans_borrower_id");

        builder.HasIndex(l => l.LoanStatusId)
            .HasDatabaseName("idx_loans_loan_status_id");

        // Drives the open/closed list filter.
        builder.HasIndex(l => l.ReturnedAt)
            .HasDatabaseName("idx_loans_returned_at");

        builder.HasOne(l => l.Item)
            .WithMany(i => i.Loans)
            .HasForeignKey(l => l.ItemId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Borrower)
            .WithMany(b => b.Loans)
            .HasForeignKey(l => l.BorrowerId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.LoanStatus)
            .WithMany(s => s.Loans)
            .HasForeignKey(l => l.LoanStatusId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
