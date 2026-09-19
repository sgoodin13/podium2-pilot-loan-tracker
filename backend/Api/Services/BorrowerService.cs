using Api.Data.Entities;
using Api.Data.Repositories;
using Api.Dtos;

namespace Api.Services;

public interface IBorrowerService
{
    Task<PagedResult<BorrowerResponse>> ListAsync(
        PageRequest page,
        string? search,
        SortRequest sort = default,
        CancellationToken ct = default);

    Task<IReadOnlyList<BorrowerResponse>> ListActiveAsync(string? search, CancellationToken ct = default);
    Task<BorrowerResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LoanResponse>> GetLoanHistoryAsync(Guid id, CancellationToken ct = default);
    Task<BorrowerResponse> CreateAsync(CreateBorrowerRequest request, CancellationToken ct = default);
    Task<BorrowerResponse> UpdateAsync(Guid id, UpdateBorrowerRequest request, CancellationToken ct = default);
    Task<BorrowerResponse> DeactivateAsync(Guid id, CancellationToken ct = default);
}

public class BorrowerService(
    IBorrowerRepository borrowers,
    ILoanRepository loans,
    ILogger<BorrowerService> logger) : IBorrowerService
{
    public async Task<PagedResult<BorrowerResponse>> ListAsync(
        PageRequest page,
        string? search,
        SortRequest sort = default,
        CancellationToken ct = default)
    {
        var (rows, total) = await borrowers.GetPagedAsync(page, search: search, sort: sort, cancellationToken: ct);

        // One query for the whole page, not one per borrower — the "Open loans"
        // column must not put this list on the N+1 path.
        var openCounts = await loans.GetOpenLoanCountsByBorrowerAsync(
            rows.Select(b => b.Id),
            ct);

        return new PagedResult<BorrowerResponse>(
            rows
                .Select(b => BorrowerResponse.From(
                    b,
                    openCounts.TryGetValue(b.Id, out var count) ? count : 0))
                .ToList(),
            total,
            page.SafePage,
            page.SafePageSize);
    }

    /// <summary>Active borrowers only — checkout wizard step 1 (REQ-3.1).</summary>
    public async Task<IReadOnlyList<BorrowerResponse>> ListActiveAsync(
        string? search,
        CancellationToken ct = default)
    {
        var (rows, _) = await borrowers.GetPagedAsync(
            new PageRequest(1, PageRequest.MaxPageSize),
            search: search,
            includeInactive: false,
            cancellationToken: ct);

        return rows.Select(b => BorrowerResponse.From(b, 0)).ToList();
    }

    public async Task<BorrowerResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var borrower = await borrowers.GetByIdAsync(id, includeInactive: true, cancellationToken: ct)
            ?? throw DomainException.NotFound("That borrower");

        var (_, openCount) = await loans.GetPagedAsync(
            new PageRequest(1, 1),
            openOnly: true,
            borrowerId: id,
            cancellationToken: ct);

        return BorrowerResponse.From(borrower, openCount);
    }

    public async Task<IReadOnlyList<LoanResponse>> GetLoanHistoryAsync(
        Guid id,
        CancellationToken ct = default)
    {
        if (!await borrowers.ExistsAsync(id, includeInactive: true, cancellationToken: ct))
        {
            throw DomainException.NotFound("That borrower");
        }

        var (rows, _) = await loans.GetPagedAsync(
            new PageRequest(1, PageRequest.MaxPageSize),
            borrowerId: id,
            cancellationToken: ct);

        return rows.Select(LoanResponse.From).ToList();
    }

    public async Task<BorrowerResponse> CreateAsync(
        CreateBorrowerRequest request,
        CancellationToken ct = default)
    {
        var borrower = new Borrower
        {
            Name = Normalize(request.Name),
            ContactEmail = request.ContactEmail?.Trim(),
            ContactPhone = request.ContactPhone?.Trim(),
            Department = request.Department?.Trim(),
            IsActive = true,
        };

        await borrowers.AddAsync(borrower, ct);
        await borrowers.SaveChangesAsync(ct);

        logger.LogInformation("Borrower created: {BorrowerId}", borrower.Id);

        return await GetAsync(borrower.Id, ct);
    }

    public async Task<BorrowerResponse> UpdateAsync(
        Guid id,
        UpdateBorrowerRequest request,
        CancellationToken ct = default)
    {
        var borrower = await borrowers.GetByIdAsync(id, includeInactive: true, cancellationToken: ct)
            ?? throw DomainException.NotFound("That borrower");

        borrower.Name = Normalize(request.Name);
        borrower.ContactEmail = request.ContactEmail?.Trim();
        borrower.ContactPhone = request.ContactPhone?.Trim();
        borrower.Department = request.Department?.Trim();

        borrowers.Update(borrower);
        await borrowers.SaveChangesAsync(ct);

        logger.LogInformation("Borrower updated: {BorrowerId}", borrower.Id);

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Deactivates (soft-deletes) a borrower.
    /// </summary>
    /// <remarks>
    /// HARD BLOCK while the borrower holds an open loan — the mirror of the item
    /// retire guard, same SME reasoning (BR §8, resolved): you do not close the
    /// account before the laptop comes back.
    /// </remarks>
    public async Task<BorrowerResponse> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var borrower = await borrowers.GetByIdAsync(id, includeInactive: true, cancellationToken: ct)
            ?? throw DomainException.NotFound("That borrower");

        var (openLoans, openCount) = await loans.GetPagedAsync(
            new PageRequest(1, PageRequest.MaxPageSize),
            openOnly: true,
            borrowerId: id,
            cancellationToken: ct);

        if (openCount > 0)
        {
            // Name the specific items, not just a count — the confirmation has to
            // be actionable (SME: "a drill" is not accountable).
            var held = string.Join(
                ", ",
                openLoans.Select(l => $"{l.Item?.Name} ({l.Item?.AssetTag})"));

            throw DomainException.Unprocessable(
                $"{borrower.Name} has {openCount} open loan{(openCount == 1 ? "" : "s")} ({held}) "
                + "and cannot be deactivated. Return the item first.");
        }

        if (!borrower.IsActive)
        {
            return await GetAsync(id, ct);
        }

        borrower.IsActive = false;
        borrowers.Update(borrower);
        await borrowers.SaveChangesAsync(ct);

        logger.LogInformation("Borrower deactivated: {BorrowerId}", borrower.Id);

        return await GetAsync(id, ct);
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
