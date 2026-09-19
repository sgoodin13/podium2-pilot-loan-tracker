using Api.Data.Repositories;
using Api.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Loan tracking (REQ-4.1 / 4.2).</summary>
[ApiController]
[Route("api/loans")]
[Produces("application/json")]
public class LoansController(ILoanService loans) : ControllerBase
{
    /// <summary>Paged loan list, filterable by state (open/closed), borrower and item.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<LoanResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LoanResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] string? state,
        [FromQuery] Guid? borrowerId,
        [FromQuery] Guid? itemId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
        => Ok(await loans.ListAsync(
            new PageRequest(page, pageSize),
            search,
            state,
            borrowerId,
            itemId,
            SortRequest.From(sortBy, sortDir),
            ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoanResponse>> Get(Guid id, CancellationToken ct = default)
        => Ok(await loans.GetAsync(id, ct));

    /// <summary>
    /// Closes a loan with a terminal status (REQ-4.2). Returns 422 when the chosen
    /// status is not terminal — a non-terminal status cannot close a loan (BR §6).
    /// </summary>
    [HttpPost("{id:guid}/return")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LoanResponse>> Return(
        Guid id,
        [FromBody] ReturnLoanRequest request,
        CancellationToken ct = default)
        => Ok(await loans.ReturnAsync(id, request, ct));
}

/// <summary>
/// The checkout process (REQ-3.1 – REQ-3.4) — this product's one real
/// business-rule endpoint.
/// </summary>
/// <remarks>
/// Separate from <see cref="LoansController"/> on purpose: a Loan is never created
/// by a generic POST /api/loans. Creation happens exclusively through the Checkout
/// process (Functional Spec: "No add screen for Loan"), so the route names the
/// process rather than the resource.
/// </remarks>
[ApiController]
[Route("api/checkout")]
[Produces("application/json")]
public class CheckoutController(ILoanService loans) : ControllerBase
{
    /// <summary>
    /// Commits a checkout.
    /// </summary>
    /// <remarks>
    /// Returns <b>409 Conflict</b> with RFC 7807 problem details when the item
    /// acquired an open loan between wizard step 2 and commit. That rejection
    /// comes from the database's filtered unique index <c>ux_loans_item_open</c>,
    /// not from an application pre-check — which is what makes BR-1 an atomic,
    /// commit-time guarantee (trigger spec §4).
    ///
    /// On rejection nothing is persisted: the insert fails atomically, so there is
    /// no partial Loan.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LoanResponse>> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken ct = default)
    {
        var loan = await loans.CheckoutAsync(request, ct);

        return CreatedAtAction(
            actionName: nameof(LoansController.Get),
            controllerName: "Loans",
            routeValues: new { id = loan.Id },
            value: loan);
    }
}
