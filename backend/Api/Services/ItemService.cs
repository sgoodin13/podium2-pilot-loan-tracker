using Api.Data.Entities;
using Api.Data.Repositories;
using Api.Dtos;

namespace Api.Services;

public interface IItemService
{
    Task<PagedResult<ItemResponse>> ListAsync(
        PageRequest page,
        string? search,
        Guid? categoryId,
        string? availability,
        SortRequest sort = default,
        CancellationToken ct = default);

    Task<IReadOnlyList<ItemResponse>> ListAvailableAsync(string? search, CancellationToken ct = default);
    Task<ItemResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LoanResponse>> GetLoanHistoryAsync(Guid id, CancellationToken ct = default);
    Task<ItemResponse> CreateAsync(CreateItemRequest request, CancellationToken ct = default);
    Task<ItemResponse> UpdateAsync(Guid id, UpdateItemRequest request, CancellationToken ct = default);
    Task<ItemResponse> RetireAsync(Guid id, CancellationToken ct = default);
}

public class ItemService(
    IItemRepository items,
    ILoanRepository loans,
    IItemCategoryRepository categories,
    ILogger<ItemService> logger) : IItemService
{
    public async Task<PagedResult<ItemResponse>> ListAsync(
        PageRequest page,
        string? search,
        Guid? categoryId,
        string? availability,
        SortRequest sort = default,
        CancellationToken ct = default)
    {
        bool? onLoanOnly = availability?.ToLowerInvariant() switch
        {
            "onloan" => true,
            "available" => false,
            _ => null,
        };

        var (rows, total) = await items.GetPagedAsync(
            page,
            search: search,
            itemCategoryId: categoryId,
            onLoanOnly: onLoanOnly,
            sort: sort,
            cancellationToken: ct);

        // One query for the whole page rather than one per row — the repository
        // exposes this specifically to keep the derived-availability column off
        // the N+1 path.
        var onLoanIds = await loans.GetItemIdsWithOpenLoanAsync(rows.Select(i => i.Id), ct);

        return new PagedResult<ItemResponse>(
            rows.Select(i => ItemResponse.From(i, onLoanIds.Contains(i.Id))).ToList(),
            total,
            page.SafePage,
            page.SafePageSize);
    }

    /// <summary>
    /// Items with zero open loans — checkout wizard step 2.
    /// </summary>
    /// <remarks>
    /// This list is a snapshot, deliberately. It narrows what Staff can pick; it
    /// does not guarantee the item is still free at commit. That guarantee is the
    /// database's, via <c>ux_loans_item_open</c>.
    /// </remarks>
    public async Task<IReadOnlyList<ItemResponse>> ListAvailableAsync(
        string? search,
        CancellationToken ct = default)
    {
        var (rows, _) = await items.GetPagedAsync(
            new PageRequest(1, PageRequest.MaxPageSize),
            search: search,
            onLoanOnly: false,
            cancellationToken: ct);

        return rows.Select(i => ItemResponse.From(i, isOnLoan: false)).ToList();
    }

    public async Task<ItemResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var item = await items.GetByIdAsync(id, includeInactive: true, cancellationToken: ct)
            ?? throw DomainException.NotFound("That item");

        var openLoan = await loans.GetOpenLoanForItemAsync(id, ct);

        return ItemResponse.From(item, openLoan);
    }

    public async Task<IReadOnlyList<LoanResponse>> GetLoanHistoryAsync(
        Guid id,
        CancellationToken ct = default)
    {
        if (!await items.ExistsAsync(id, includeInactive: true, cancellationToken: ct))
        {
            throw DomainException.NotFound("That item");
        }

        var (rows, _) = await loans.GetPagedAsync(
            new PageRequest(1, PageRequest.MaxPageSize),
            itemId: id,
            cancellationToken: ct);

        return rows.Select(LoanResponse.From).ToList();
    }

    public async Task<ItemResponse> CreateAsync(
        CreateItemRequest request,
        CancellationToken ct = default)
    {
        var name = Normalize(request.Name, "Name");
        var assetTag = Normalize(request.AssetTag, "Asset tag");

        var category = await categories.GetByIdAsync(request.ItemCategoryId, cancellationToken: ct)
            ?? throw DomainException.Unprocessable("That category does not exist.");

        if (!category.IsActive)
        {
            throw DomainException.Unprocessable(
                $"The category '{category.Name}' is deactivated and cannot be assigned to a new item.");
        }

        if (await items.AssetTagExistsAsync(assetTag, cancellationToken: ct))
        {
            throw DomainException.Conflict(
                $"An item with asset tag '{assetTag}' already exists. Asset tags must be unique.");
        }

        var item = new Item
        {
            Name = name,
            Description = request.Description?.Trim(),
            AssetTag = assetTag,
            ItemCategoryId = category.Id,
            IsActive = true,
        };

        await items.AddAsync(item, ct);
        await items.SaveChangesAsync(ct);

        logger.LogInformation("Item created: {AssetTag} ({ItemId})", item.AssetTag, item.Id);

        return await GetAsync(item.Id, ct);
    }

    public async Task<ItemResponse> UpdateAsync(
        Guid id,
        UpdateItemRequest request,
        CancellationToken ct = default)
    {
        var item = await items.GetByIdAsync(id, includeInactive: true, cancellationToken: ct)
            ?? throw DomainException.NotFound("That item");

        var name = Normalize(request.Name, "Name");
        var assetTag = Normalize(request.AssetTag, "Asset tag");

        var category = await categories.GetByIdAsync(request.ItemCategoryId, cancellationToken: ct)
            ?? throw DomainException.Unprocessable("That category does not exist.");

        if (await items.AssetTagExistsAsync(assetTag, excludeId: id, cancellationToken: ct))
        {
            throw DomainException.Conflict(
                $"An item with asset tag '{assetTag}' already exists. Asset tags must be unique.");
        }

        item.Name = name;
        item.Description = request.Description?.Trim();
        item.AssetTag = assetTag;
        item.ItemCategoryId = category.Id;

        items.Update(item);
        await items.SaveChangesAsync(ct);

        logger.LogInformation("Item updated: {AssetTag} ({ItemId})", item.AssetTag, item.Id);

        return await GetAsync(item.Id, ct);
    }

    /// <summary>
    /// Retires (soft-deletes) an item.
    /// </summary>
    /// <remarks>
    /// HARD BLOCK while the item has an open loan — not a warning. Per the SME
    /// ruling (BR §8, resolved): a warning is something a person clicks past, and
    /// then a still-open loan is attached to a retired item that nobody can close
    /// out cleanly. The legitimate "it is genuinely gone" case already has a door:
    /// close the loan with the Lost terminal status first, then retire.
    /// </remarks>
    public async Task<ItemResponse> RetireAsync(Guid id, CancellationToken ct = default)
    {
        var item = await items.GetByIdAsync(id, includeInactive: true, cancellationToken: ct)
            ?? throw DomainException.NotFound("That item");

        var openLoan = await loans.GetOpenLoanForItemAsync(id, ct);
        if (openLoan is not null)
        {
            var borrowerName = openLoan.Borrower?.Name ?? "a borrower";
            throw DomainException.Unprocessable(
                $"{item.Name} ({item.AssetTag}) has an open loan to {borrowerName} and cannot be retired. "
                + "Return it first.");
        }

        if (!item.IsActive)
        {
            return await GetAsync(id, ct);
        }

        item.IsActive = false;
        items.Update(item);
        await items.SaveChangesAsync(ct);

        logger.LogInformation("Item retired: {AssetTag} ({ItemId})", item.AssetTag, item.Id);

        return await GetAsync(id, ct);
    }

    /// <summary>Trims and rejects whitespace-only input.</summary>
    private static string Normalize(string value, string fieldName)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw DomainException.Unprocessable($"{fieldName} cannot be blank.");
        }

        return trimmed;
    }
}
