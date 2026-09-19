using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.Middleware;

/// <summary>
/// Stamps the four audit columns on every insert and update.
/// </summary>
/// <remarks>
/// Centralised here rather than repeated in each service: the Database Engineer
/// flagged that <c>created_by</c> is NOT NULL with a CLR default of <c>""</c>, so a
/// service that forgot to stamp it would insert a blank rather than fail loudly.
/// An interceptor makes forgetting impossible.
///
/// The user label comes from the stub auth principal. It is a plain text value —
/// there is no identity store to foreign-key against (PDM, auth-stub decision).
/// </remarks>
public class AuditStampingInterceptor(IHttpContextAccessor httpContextAccessor)
    : SaveChangesInterceptor
{
    private const string SystemUser = "system";

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Stamp(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Stamp(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void Stamp(DbContext context)
    {
        var user = httpContextAccessor.HttpContext?.User?.Identity?.Name ?? SystemUser;
        var now = DateTimeOffset.UtcNow;

        foreach (EntityEntry<EntityBase> entry in context.ChangeTracker.Entries<EntityBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = user;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedAt = now;
                    entry.Entity.ModifiedBy = user;
                    // Creation facts are immutable once written.
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    break;

                case EntityState.Deleted:
                    // Soft-delete only — no entity is ever physically removed
                    // (Products/LoanTracker.json domain_rulings: no hard-delete
                    // exceptions for this product). Convert any stray hard delete
                    // into a deactivation rather than letting it through.
                    entry.State = EntityState.Modified;
                    entry.Entity.IsActive = false;
                    entry.Entity.ModifiedAt = now;
                    entry.Entity.ModifiedBy = user;
                    break;
            }
        }
    }
}
