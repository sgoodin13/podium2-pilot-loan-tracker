using Api.Data.Entities;
using Api.Data.Repositories;
using Api.Data.Seed;
using Api.Dtos;

namespace Api.Services;

public interface IItemCategoryService
{
    Task<IReadOnlyList<ItemCategoryResponse>> ListAsync(bool includeInactive, CancellationToken ct = default);
    Task<ItemCategoryResponse> CreateAsync(ItemCategoryRequest request, CancellationToken ct = default);
    Task<ItemCategoryResponse> UpdateAsync(Guid id, ItemCategoryRequest request, CancellationToken ct = default);
}

public interface ILoanStatusService
{
    Task<IReadOnlyList<LoanStatusResponse>> ListAsync(
        bool includeInactive,
        bool terminalOnly,
        CancellationToken ct = default);

    Task<LoanStatusResponse> CreateAsync(LoanStatusRequest request, CancellationToken ct = default);
    Task<LoanStatusResponse> UpdateAsync(Guid id, LoanStatusRequest request, CancellationToken ct = default);
}

/// <summary>
/// Item Category maintenance (REQ-5.1).
/// </summary>
/// <remarks>
/// Reference rows are deactivated, never hard-deleted — BR §5.5 and the product's
/// universal soft-delete ruling. There is no delete operation on this service at
/// all; deactivation is expressed as <c>IsActive = false</c> on update.
/// </remarks>
public class ItemCategoryService(
    IItemCategoryRepository categories,
    ILogger<ItemCategoryService> logger) : IItemCategoryService
{
    public async Task<IReadOnlyList<ItemCategoryResponse>> ListAsync(
        bool includeInactive,
        CancellationToken ct = default)
    {
        var (rows, _) = await categories.GetPagedAsync(
            new PageRequest(1, PageRequest.MaxPageSize),
            includeInactive: includeInactive,
            cancellationToken: ct);

        // One batched count for the page — the UI needs it to name the effect of
        // deactivating a category before the save (Compliance finding F5).
        var counts = await categories.GetActiveItemCountsAsync(rows.Select(c => c.Id), ct);

        return rows
            .Select(c => ItemCategoryResponse.From(c, counts.TryGetValue(c.Id, out var n) ? n : 0))
            .ToList();
    }

    public async Task<ItemCategoryResponse> CreateAsync(
        ItemCategoryRequest request,
        CancellationToken ct = default)
    {
        var name = Normalize(request.Name);

        if (await categories.NameExistsAsync(name, cancellationToken: ct))
        {
            throw DomainException.Conflict($"A category named '{name}' already exists.");
        }

        var category = new ItemCategory
        {
            Name = name,
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
        };

        await categories.AddAsync(category, ct);
        await categories.SaveChangesAsync(ct);

        logger.LogInformation("Item category created: {CategoryId}", category.Id);

        return ItemCategoryResponse.From(category);
    }

    public async Task<ItemCategoryResponse> UpdateAsync(
        Guid id,
        ItemCategoryRequest request,
        CancellationToken ct = default)
    {
        var category = await categories.GetByIdAsync(id, includeInactive: true, cancellationToken: ct)
            ?? throw DomainException.NotFound("That category");

        var name = Normalize(request.Name);

        if (await categories.NameExistsAsync(name, excludeId: id, cancellationToken: ct))
        {
            throw DomainException.Conflict($"A category named '{name}' already exists.");
        }

        // A category still referenced by an active item may be deactivated but the
        // effect is stated rather than silent — items keep their reference, and the
        // category simply stops being offered for new items.
        if (category.IsActive && !request.IsActive
            && await categories.HasActiveItemsAsync(id, ct))
        {
            logger.LogWarning(
                "Item category {CategoryId} deactivated while still referenced by active items",
                id);
        }

        category.Name = name;
        category.Description = request.Description?.Trim();
        category.IsActive = request.IsActive;

        categories.Update(category);
        await categories.SaveChangesAsync(ct);

        return ItemCategoryResponse.From(category);
    }

    private static string Normalize(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw DomainException.Unprocessable("Name cannot be blank.");
        }

        return trimmed;
    }
}

/// <summary>
/// Loan Status maintenance (REQ-5.2), including the Is Terminal flag.
/// </summary>
/// <remarks>
/// Statuses are maintained reference data, not a hardcoded enum: Staff can add a
/// further terminal status (BR §4 gives "Returned – Needs Repair" as the worked
/// example) without a code change.
/// </remarks>
public class LoanStatusService(
    ILoanStatusRepository statuses,
    ILogger<LoanStatusService> logger) : ILoanStatusService
{
    public async Task<IReadOnlyList<LoanStatusResponse>> ListAsync(
        bool includeInactive,
        bool terminalOnly,
        CancellationToken ct = default)
    {
        var (rows, _) = await statuses.GetPagedAsync(
            new PageRequest(1, PageRequest.MaxPageSize),
            includeInactive: includeInactive,
            cancellationToken: ct);

        var filtered = terminalOnly
            ? rows.Where(s => s.IsTerminal).ToList()
            : rows.ToList();

        return filtered.Select(LoanStatusResponse.From).ToList();
    }

    public async Task<LoanStatusResponse> CreateAsync(
        LoanStatusRequest request,
        CancellationToken ct = default)
    {
        var name = Normalize(request.Name);

        if (await statuses.NameExistsAsync(name, cancellationToken: ct))
        {
            throw DomainException.Conflict($"A loan status named '{name}' already exists.");
        }

        var status = new LoanStatus
        {
            Name = name,
            Description = request.Description?.Trim(),
            IsTerminal = request.IsTerminal,
            IsActive = request.IsActive,
        };

        await statuses.AddAsync(status, ct);
        await statuses.SaveChangesAsync(ct);

        logger.LogInformation(
            "Loan status created: {StatusId} (terminal: {IsTerminal})",
            status.Id,
            status.IsTerminal);

        return LoanStatusResponse.From(status);
    }

    public async Task<LoanStatusResponse> UpdateAsync(
        Guid id,
        LoanStatusRequest request,
        CancellationToken ct = default)
    {
        var status = await statuses.GetByIdAsync(id, includeInactive: true, cancellationToken: ct)
            ?? throw DomainException.NotFound("That loan status");

        var name = Normalize(request.Name);

        if (await statuses.NameExistsAsync(name, excludeId: id, cancellationToken: ct))
        {
            throw DomainException.Conflict($"A loan status named '{name}' already exists.");
        }

        await GuardReferencedStatusAsync(status, request, name, ct);

        status.Name = name;
        status.Description = request.Description?.Trim();
        status.IsTerminal = request.IsTerminal;
        status.IsActive = request.IsActive;

        statuses.Update(status);
        await statuses.SaveChangesAsync(ct);

        return LoanStatusResponse.From(status);
    }

    /// <summary>
    /// Refuses the reference-data edits that would disable checkout or return
    /// product-wide (Compliance finding F2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="LoanService"/> resolves the open-loan status by name, and
    /// <c>GetByNameAsync</c> only returns active rows — so renaming or deactivating
    /// "Checked Out" silently breaks every checkout, and the error the user would
    /// then see misdirects them to reference-data seeding. Likewise, deactivating the
    /// last terminal status empties the return dropdown and leaves every open loan
    /// permanently uncloseable, which in turn leaves its item permanently blocked
    /// from retire by the hard guard.
    /// </para>
    /// <para>
    /// The checkout status is matched on its stable seeded id rather than its
    /// display name, so the guard does not itself depend on the editable field.
    /// </para>
    /// </remarks>
    private async Task GuardReferencedStatusAsync(
        LoanStatus status,
        LoanStatusRequest request,
        string newName,
        CancellationToken ct)
    {
        var isCheckoutStatus = status.Id == SeedData.StatusIds.CheckedOut;

        if (isCheckoutStatus)
        {
            if (!string.Equals(status.Name, newName, StringComparison.Ordinal))
            {
                throw DomainException.Unprocessable(
                    $"'{status.Name}' is the status the checkout process assigns, so it cannot be "
                    + "renamed. Renaming it would stop every checkout in the product.");
            }

            if (!request.IsActive)
            {
                throw DomainException.Unprocessable(
                    $"'{status.Name}' is the status the checkout process assigns, so it cannot be "
                    + "deactivated. Deactivating it would stop every checkout in the product.");
            }

            if (request.IsTerminal)
            {
                throw DomainException.Unprocessable(
                    $"'{status.Name}' marks a loan as open, so it cannot be marked terminal.");
            }
        }

        var beingDeactivated = status.IsActive && !request.IsActive;

        if (beingDeactivated && await statuses.HasOpenLoansAsync(status.Id, ct))
        {
            throw DomainException.Unprocessable(
                $"'{status.Name}' is the current status of at least one open loan, so it cannot be "
                + "deactivated. Close those loans first.");
        }

        // Losing the last terminal status leaves no way to close a loan at all.
        var losingTerminal = status.IsActive && status.IsTerminal
            && (!request.IsTerminal || !request.IsActive);

        if (losingTerminal && await statuses.CountOtherActiveTerminalAsync(status.Id, ct) == 0)
        {
            throw DomainException.Unprocessable(
                $"'{status.Name}' is the only status that can close a loan. Add or reactivate "
                + "another terminal status before changing this one.");
        }
    }

    private static string Normalize(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw DomainException.Unprocessable("Name cannot be blank.");
        }

        return trimmed;
    }
}
