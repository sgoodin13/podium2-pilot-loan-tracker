using Api.Data.Repositories;
using Api.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Borrower management (REQ-2.1 / 2.2 / 2.3).</summary>
[ApiController]
[Route("api/borrowers")]
[Produces("application/json")]
public class BorrowersController(IBorrowerService borrowers) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<BorrowerResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BorrowerResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
        => Ok(await borrowers.ListAsync(
            new PageRequest(page, pageSize),
            search,
            SortRequest.From(sortBy, sortDir),
            ct));

    /// <summary>Active borrowers only — checkout wizard step 1 (REQ-3.1).</summary>
    [HttpGet("active")]
    [ProducesResponseType<IReadOnlyList<BorrowerResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BorrowerResponse>>> Active(
        [FromQuery] string? search,
        CancellationToken ct = default)
        => Ok(await borrowers.ListActiveAsync(search, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BorrowerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BorrowerResponse>> Get(Guid id, CancellationToken ct = default)
        => Ok(await borrowers.GetAsync(id, ct));

    /// <summary>The borrower's current and historical loans (REQ-2.3).</summary>
    [HttpGet("{id:guid}/loans")]
    [ProducesResponseType<IReadOnlyList<LoanResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LoanResponse>>> LoanHistory(
        Guid id,
        CancellationToken ct = default)
        => Ok(await borrowers.GetLoanHistoryAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<BorrowerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BorrowerResponse>> Create(
        [FromBody] CreateBorrowerRequest request,
        CancellationToken ct = default)
    {
        var created = await borrowers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<BorrowerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BorrowerResponse>> Update(
        Guid id,
        [FromBody] UpdateBorrowerRequest request,
        CancellationToken ct = default)
        => Ok(await borrowers.UpdateAsync(id, request, ct));

    /// <summary>
    /// Deactivates (soft-deletes) a borrower. Returns 422 with the specific items
    /// still held while the borrower has an open loan — a hard block (BR §8).
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<BorrowerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BorrowerResponse>> Deactivate(
        Guid id,
        CancellationToken ct = default)
        => Ok(await borrowers.DeactivateAsync(id, ct));
}
