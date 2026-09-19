using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Api.Middleware;

/// <summary>
/// Writes an RFC 7807 payload with the <c>application/problem+json</c> media type,
/// guaranteed.
/// </summary>
/// <remarks>
/// Setting <c>ObjectResult.ContentTypes</c> is not enough on its own: the JSON
/// output formatter matches <c>application/*+json</c> and then writes its own
/// default <c>application/json</c>, so model-validation failures left with a
/// different media type than the ones the exception middleware produced (QA
/// defect D3). Serialising here removes the negotiation step entirely, so both
/// error paths agree.
/// </remarks>
public class ProblemJsonResult(ProblemDetails problem) : IActionResult
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;

        response.StatusCode = problem.Status ?? StatusCodes.Status400BadRequest;
        response.ContentType = "application/problem+json";

        await response.WriteAsync(JsonSerializer.Serialize(problem, problem.GetType(), Options));
    }
}
