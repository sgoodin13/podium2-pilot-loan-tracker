using Api.Data.Repositories;
using Api.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Item management (REQ-1.1 / 1.2 / 1.3).
/// </summary>
/// <remarks>
/// Controllers hold no business logic — they bind, delegate to a service, and
/// shape the HTTP response (LoanTracker_Stack_Rules.md [STACK_RULES]).
/// </remarks>
[ApiController]
[Route("api/items")]
[Produces("application/json")]
public class ItemsController(IItemService items) : ControllerBase
{
    /// <summary>Paged item list, filterable by search, category and availability.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ItemResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? availability,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
        => Ok(await items.ListAsync(
            new PageRequest(page, pageSize),
            search,
            categoryId,
            availability,
            SortRequest.From(sortBy, sortDir),
            ct));

    /// <summary>Items with zero open loans — checkout wizard step 2 (REQ-3.2).</summary>
    [HttpGet("available")]
    [ProducesResponseType<IReadOnlyList<ItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ItemResponse>>> Available(
        [FromQuery] string? search,
        CancellationToken ct = default)
        => Ok(await items.ListAvailableAsync(search, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemResponse>> Get(Guid id, CancellationToken ct = default)
        => Ok(await items.GetAsync(id, ct));

    /// <summary>The item's full loan history, most recent first (REQ-1.3).</summary>
    [HttpGet("{id:guid}/loans")]
    [ProducesResponseType<IReadOnlyList<LoanResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LoanResponse>>> LoanHistory(
        Guid id,
        CancellationToken ct = default)
        => Ok(await items.GetLoanHistoryAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<ItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ItemResponse>> Create(
        [FromBody] CreateItemRequest request,
        CancellationToken ct = default)
    {
        var created = await items.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ItemResponse>> Update(
        Guid id,
        [FromBody] UpdateItemRequest request,
        CancellationToken ct = default)
        => Ok(await items.UpdateAsync(id, request, ct));

    /// <summary>
    /// Retires (soft-deletes) an item. Returns 422 with a specific reason while
    /// the item has an open loan — a hard block, per the SME ruling (BR §8).
    /// </summary>
    [HttpPost("{id:guid}/retire")]
    [ProducesResponseType<ItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ItemResponse>> Retire(Guid id, CancellationToken ct = default)
        => Ok(await items.RetireAsync(id, ct));
}
