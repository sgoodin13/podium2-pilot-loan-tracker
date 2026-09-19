using Api.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Item Category maintenance (REQ-5.1).</summary>
/// <remarks>
/// There is no DELETE action by design: reference rows are deactivated, never
/// hard-deleted (BR §5.5, and the product's universal soft-delete ruling).
/// </remarks>
[ApiController]
[Route("api/item-categories")]
[Produces("application/json")]
public class ItemCategoriesController(IItemCategoryService categories) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ItemCategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ItemCategoryResponse>>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
        => Ok(await categories.ListAsync(includeInactive, ct));

    [HttpPost]
    [ProducesResponseType<ItemCategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ItemCategoryResponse>> Create(
        [FromBody] ItemCategoryRequest request,
        CancellationToken ct = default)
    {
        var created = await categories.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ItemCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ItemCategoryResponse>> Update(
        Guid id,
        [FromBody] ItemCategoryRequest request,
        CancellationToken ct = default)
        => Ok(await categories.UpdateAsync(id, request, ct));
}

/// <summary>Loan Status maintenance (REQ-5.2), including the Is Terminal flag.</summary>
[ApiController]
[Route("api/loan-statuses")]
[Produces("application/json")]
public class LoanStatusesController(ILoanStatusService statuses) : ControllerBase
{
    /// <summary>
    /// Lists loan statuses. <paramref name="terminalOnly"/> returns just the set
    /// that can legally close a loan — the loan-detail return panel uses it.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LoanStatusResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LoanStatusResponse>>> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool terminalOnly = false,
        CancellationToken ct = default)
        => Ok(await statuses.ListAsync(includeInactive, terminalOnly, ct));

    [HttpPost]
    [ProducesResponseType<LoanStatusResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanStatusResponse>> Create(
        [FromBody] LoanStatusRequest request,
        CancellationToken ct = default)
    {
        var created = await statuses.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<LoanStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanStatusResponse>> Update(
        Guid id,
        [FromBody] LoanStatusRequest request,
        CancellationToken ct = default)
        => Ok(await statuses.UpdateAsync(id, request, ct));
}
