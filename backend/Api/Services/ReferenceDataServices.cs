using Api.Data.Entities;
using Api.Data.Repositories;
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

        return rows.Select(ItemCategoryResponse.From).ToList();
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

        status.Name = name;
        status.Description = request.Description?.Trim();
        status.IsTerminal = request.IsTerminal;
        status.IsActive = request.IsActive;

        statuses.Update(status);
        await statuses.SaveChangesAsync(ct);

        return LoanStatusResponse.From(status);
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
