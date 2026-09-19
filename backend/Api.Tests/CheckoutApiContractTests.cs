using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Dtos;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

/// <summary>
/// Direct-API contract tests on POST /api/checkout — this product's one real
/// business-rule endpoint — plus the RFC 7807 error format every failure must use.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CheckoutApiContractTests(PostgresFixture fixture) : IAsyncLifetime
{
    private LoanTrackerApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new LoanTrackerApiFactory(fixture.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<(Guid ItemId, string AssetTag, string ItemName, Guid BorrowerId)> AvailablePairAsync()
    {
        var category = await fixture.CategoryAsync();
        await using var db = fixture.CreateContext();
        var item = await TestData.AddItemAsync(db, category.Id);
        var borrower = await TestData.AddBorrowerAsync(db);
        return (item.Id, item.AssetTag, item.Name, borrower.Id);
    }

    // --- Success shape -----------------------------------------------------

    [Fact]
    public async Task Checkout_answers_201_with_a_location_header_and_the_full_loan_shape()
    {
        var (itemId, assetTag, itemName, borrowerId) = await AvailablePairAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest { BorrowerId = borrowerId, ItemId = itemId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull("the created loan must be addressable");

        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>(Json);
        loan.Should().NotBeNull();
        loan!.Id.Should().NotBeEmpty();
        loan.ItemId.Should().Be(itemId);
        loan.ItemAssetTag.Should().Be(assetTag);
        loan.ItemName.Should().Be(itemName);
        loan.BorrowerId.Should().Be(borrowerId);
        loan.BorrowerName.Should().NotBeNullOrWhiteSpace();
        loan.LoanStatusName.Should().Be("Checked Out");
        loan.LoanStatusIsTerminal.Should().BeFalse();
        loan.ReturnedAt.Should().BeNull();
        loan.IsOpen.Should().BeTrue();
        loan.CheckedOutAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));

        response.Headers.Location!.ToString().Should().Contain(loan.Id.ToString());
    }

    // --- The blocked path, over HTTP ---------------------------------------

    /// <summary>
    /// The BR-1 rejection as the wizard sees it: 409, problem+json, and a detail
    /// naming the item and asset tag. Nothing persisted.
    /// </summary>
    [Fact]
    public async Task Second_checkout_of_the_same_item_answers_409_problem_details_naming_the_item()
    {
        var (itemId, assetTag, itemName, borrowerId) = await AvailablePairAsync();

        await using var db = fixture.CreateContext();
        var second = await TestData.AddBorrowerAsync(db);

        var first = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest { BorrowerId = borrowerId, ItemId = itemId });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var conflict = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest { BorrowerId = second.Id, ItemId = itemId });

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        conflict.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await ReadProblemAsync(conflict);
        problem.Title.Should().Be("Conflict");
        problem.Status.Should().Be(409);
        problem.Detail.Should().Contain(itemName);
        problem.Detail.Should().Contain(assetTag);
        problem.Instance.Should().Be("/api/checkout");
        problem.TraceId.Should().NotBeNullOrWhiteSpace("the trace id ties a report back to the server log");

        // Out-of-band: exactly one loan exists for the item.
        await using var verify = fixture.CreateContext();
        (await verify.Loans.CountAsync(l => l.ItemId == itemId)).Should().Be(1);
    }

    // --- Negative paths on the checkout endpoint ---------------------------

    [Fact]
    public async Task Checkout_with_a_malformed_payload_answers_400_problem_details()
    {
        var response = await _client.PostAsync(
            "/api/checkout",
            new StringContent("{ this is not json", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        problem.Status.Should().Be(400);
        problem.Title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Checkout_with_an_empty_body_answers_400()
    {
        var response = await _client.PostAsync(
            "/api/checkout",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Missing ids are a validation failure, not a lookup miss.
    /// </summary>
    /// <remarks>
    /// Was DEFECT D1: <c>[Required]</c> cannot fail on a non-nullable <c>Guid</c>, so an
    /// empty body bound to <c>Guid.Empty</c>, satisfied the attribute and fell through to
    /// the existence check — surfacing as a confusing 404. The ids are nullable now, so
    /// the attribute does its job.
    /// </remarks>
    [Fact]
    public async Task Checkout_with_missing_required_fields_answers_400_with_field_errors()
    {
        var response = await _client.PostAsync(
            "/api/checkout",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "a missing id is a validation failure, not a missing record");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("A borrower is required.");
        body.Should().Contain("An item is required.");
    }

    [Fact]
    public async Task Checkout_of_an_unknown_item_answers_404_problem_details()
    {
        await using var db = fixture.CreateContext();
        var borrower = await TestData.AddBorrowerAsync(db);

        var response = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest { BorrowerId = borrower.Id, ItemId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadProblemAsync(response)).Detail.Should().Contain("item");
    }

    /// <summary>
    /// A deactivated borrower is blocked over HTTP with the real reason (was DEFECT D2).
    /// </summary>
    /// <remarks>
    /// The checkout was always correctly BLOCKED; only the stated reason was wrong. It
    /// answered 404 "That borrower was not found." because the lookup filtered inactive
    /// rows before the guard could run. With <c>includeInactive: true</c> the 422 branch
    /// is reachable and the caller is told what actually happened.
    /// </remarks>
    [Fact]
    public async Task Checkout_for_a_deactivated_borrower_answers_422_naming_the_reason()
    {
        var category = await fixture.CategoryAsync();
        await using var db = fixture.CreateContext();
        var item = await TestData.AddItemAsync(db, category.Id);
        var borrower = await TestData.AddBorrowerAsync(db, isActive: false);

        var response = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest { BorrowerId = borrower.Id, ItemId = item.Id });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await ReadProblemAsync(response);
        problem.Status.Should().Be(422);
        problem.Title.Should().Be("Rule violation");
        problem.Detail.Should().Contain(
            "deactivated",
            "the caller must be told the borrower is deactivated, not that they do not exist");

        // Whatever the message, nothing was persisted.
        await using var verify = fixture.CreateContext();
        (await verify.Loans.AnyAsync(l => l.BorrowerId == borrower.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Loan_is_never_creatable_through_a_generic_post_to_api_loans()
    {
        var (itemId, _, _, borrowerId) = await AvailablePairAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/loans",
            new CheckoutRequest { BorrowerId = borrowerId, ItemId = itemId });

        response.StatusCode.Should().Be(
            HttpStatusCode.MethodNotAllowed,
            "a Loan is created exclusively through the Checkout process — there is no add screen for Loan");
    }

    // --- Return endpoint contract ------------------------------------------

    [Fact]
    public async Task Return_with_a_non_terminal_status_answers_422_and_the_loan_stays_open()
    {
        var (itemId, _, _, borrowerId) = await AvailablePairAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");

        var created = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest { BorrowerId = borrowerId, ItemId = itemId });
        var loan = (await created.Content.ReadFromJsonAsync<LoanResponse>(Json))!;

        var response = await _client.PostAsJsonAsync(
            $"/api/loans/{loan.Id}/return",
            new ReturnLoanRequest { LoanStatusId = checkedOut.Id });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await ReadProblemAsync(response);
        problem.Detail.Should().Contain("not a terminal status");

        var reread = await _client.GetFromJsonAsync<LoanResponse>($"/api/loans/{loan.Id}", Json);
        reread!.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Return_with_a_terminal_status_answers_200_with_the_closed_loan()
    {
        var (itemId, assetTag, _, borrowerId) = await AvailablePairAsync();
        var returned = await fixture.StatusAsync("Returned");

        var created = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest { BorrowerId = borrowerId, ItemId = itemId });
        var loan = (await created.Content.ReadFromJsonAsync<LoanResponse>(Json))!;

        var response = await _client.PostAsJsonAsync(
            $"/api/loans/{loan.Id}/return",
            new ReturnLoanRequest { LoanStatusId = returned.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var closed = (await response.Content.ReadFromJsonAsync<LoanResponse>(Json))!;
        closed.IsOpen.Should().BeFalse();
        closed.ReturnedAt.Should().NotBeNull();
        closed.LoanStatusName.Should().Be("Returned");
        closed.ItemAssetTag.Should().Be(assetTag);

        // And the item reads as available again — derived, not stored.
        var item = await _client.GetFromJsonAsync<ItemResponse>($"/api/items/{itemId}", Json);
        item!.IsOnLoan.Should().BeFalse();
    }

    [Fact]
    public async Task Return_of_an_unknown_loan_answers_404()
    {
        var returned = await fixture.StatusAsync("Returned");

        var response = await _client.PostAsJsonAsync(
            $"/api/loans/{Guid.NewGuid()}/return",
            new ReturnLoanRequest { LoanStatusId = returned.Id });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Return_with_a_non_guid_route_id_answers_404_from_routing()
    {
        var returned = await fixture.StatusAsync("Returned");

        var response = await _client.PostAsJsonAsync(
            "/api/loans/not-a-guid/return",
            new ReturnLoanRequest { LoanStatusId = returned.Id });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Item / borrower create endpoints: negative-path floor --------------

    [Fact]
    public async Task Create_item_with_missing_required_fields_answers_400_with_field_keyed_errors()
    {
        var response = await _client.PostAsync(
            "/api/items",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        document.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue(
            "the Add screen renders the message inline on the offending field, so the payload must key by field");

        var keys = errors.EnumerateObject().Select(p => p.Name).ToList();
        keys.Should().Contain(k => k.Equals("Name", StringComparison.OrdinalIgnoreCase));
        keys.Should().Contain(k => k.Equals("AssetTag", StringComparison.OrdinalIgnoreCase));

        document.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
        document.RootElement.GetProperty("title").GetString().Should().Be("Validation failed");
        document.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        document.RootElement.GetProperty("instance").GetString().Should().Be("/api/items");
    }

    /// <summary>
    /// Both error paths agree on the media type (was DEFECT D3, now fixed).
    /// </summary>
    /// <remarks>
    /// Model-validation failures used to leave as <c>application/json</c> while the
    /// exception middleware emitted <c>application/problem+json</c>. The body was correctly
    /// RFC 7807 either way, so nothing was broken for the client — the CONTRACT was simply
    /// inconsistent. The validation path now writes its response explicitly, so both agree.
    /// </remarks>
    [Fact]
    public async Task Both_error_paths_leave_as_application_problem_json()
    {
        var validationFailure = await _client.PostAsync(
            "/api/items",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        var domainFailure = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest { BorrowerId = Guid.NewGuid(), ItemId = Guid.NewGuid() });

        validationFailure.Content.Headers.ContentType!.MediaType.Should().Be(
            "application/problem+json",
            "the validation path writes the response explicitly rather than negotiating it");

        domainFailure.Content.Headers.ContentType!.MediaType.Should().Be(
            "application/problem+json",
            "the middleware path sets the media type on the response directly");
    }

    [Fact]
    public async Task Create_item_with_a_whitespace_only_name_answers_400()
    {
        var category = await fixture.CategoryAsync();

        var response = await _client.PostAsJsonAsync("/api/items", new CreateItemRequest
        {
            Name = "   ",
            AssetTag = $"QA-WS-{TestData.Unique()}",
            ItemCategoryId = category.Id,
        });

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "RequiredAttribute trims, so whitespace-only is rejected at the boundary before the service's guard");
    }

    [Fact]
    public async Task Create_item_over_the_asset_tag_limit_answers_400()
    {
        var category = await fixture.CategoryAsync();

        var response = await _client.PostAsJsonAsync("/api/items", new CreateItemRequest
        {
            Name = "QA Over Limit",
            AssetTag = new string('x', FieldLimits.AssetTag + 1),
            ItemCategoryId = category.Id,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_item_with_a_duplicate_asset_tag_answers_409_and_persists_nothing()
    {
        var category = await fixture.CategoryAsync();
        var tag = $"QA-API-DUP-{TestData.Unique()}";

        var first = await _client.PostAsJsonAsync("/api/items", new CreateItemRequest
        {
            Name = "QA First",
            AssetTag = tag,
            ItemCategoryId = category.Id,
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await _client.PostAsJsonAsync("/api/items", new CreateItemRequest
        {
            Name = "QA Second",
            AssetTag = tag,
            ItemCategoryId = category.Id,
        });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadProblemAsync(duplicate)).Detail.Should().Contain(tag);

        await using var verify = fixture.CreateContext();
        (await verify.Items.CountAsync(i => i.AssetTag == tag)).Should().Be(1);
    }

    [Fact]
    public async Task Create_borrower_with_no_name_answers_400()
    {
        var response = await _client.PostAsync(
            "/api/borrowers",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_borrower_with_an_invalid_email_answers_400()
    {
        var response = await _client.PostAsJsonAsync("/api/borrowers", new CreateBorrowerRequest
        {
            Name = "QA Bad Email",
            ContactEmail = "definitely-not-an-email",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The frontend's Add-borrower form allows 50 characters of phone and 200 of
    /// department; the API's limits are 32 and 128. Asserting the API's actual
    /// verdict here is what surfaces that mismatch — see the QA report.
    /// </summary>
    [Fact]
    public async Task Create_borrower_over_the_api_phone_limit_answers_400()
    {
        var response = await _client.PostAsJsonAsync("/api/borrowers", new CreateBorrowerRequest
        {
            Name = "QA Long Phone",
            ContactPhone = new string('9', FieldLimits.Phone + 1),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_borrower_over_the_api_department_limit_answers_400()
    {
        var response = await _client.PostAsJsonAsync("/api/borrowers", new CreateBorrowerRequest
        {
            Name = "QA Long Department",
            Department = new string('d', FieldLimits.Department + 1),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Retire / deactivate guards over HTTP ------------------------------

    [Fact]
    public async Task Retire_answers_422_while_the_item_has_an_open_loan()
    {
        var (itemId, assetTag, itemName, borrowerId) = await AvailablePairAsync();

        await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest { BorrowerId = borrowerId, ItemId = itemId });

        var response = await _client.PostAsync($"/api/items/{itemId}/retire", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await ReadProblemAsync(response);
        problem.Detail.Should().Contain(itemName);
        problem.Detail.Should().Contain(assetTag);
    }

    [Fact]
    public async Task Deactivate_answers_422_while_the_borrower_holds_an_open_loan()
    {
        var (itemId, assetTag, _, borrowerId) = await AvailablePairAsync();

        await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest { BorrowerId = borrowerId, ItemId = itemId });

        var response = await _client.PostAsync($"/api/borrowers/{borrowerId}/deactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadProblemAsync(response)).Detail.Should().Contain(assetTag);
    }

    // --- Auth (stub) -------------------------------------------------------

    /// <summary>
    /// The approved scope ruling is an auth STUB: every request becomes an
    /// authenticated Staff user, and there is no anonymous or forbidden state to reach.
    /// This test asserts the stub's actual behaviour rather than faking a 401 that the
    /// product cannot produce — see the QA report's "not testable" note.
    /// </summary>
    [Fact]
    public async Task Every_request_is_authenticated_as_staff_by_the_auth_stub()
    {
        var withoutAnyCredentials = await _client.GetAsync("/api/items?pageSize=1");
        withoutAnyCredentials.StatusCode.Should().Be(HttpStatusCode.OK);

        var withNonsenseBearer = new HttpRequestMessage(HttpMethod.Get, "/api/items?pageSize=1");
        withNonsenseBearer.Headers.Add("Authorization", "Bearer not-a-real-token");
        (await _client.SendAsync(withNonsenseBearer)).StatusCode.Should().Be(HttpStatusCode.OK);

        // The stub's principal is what the audit columns record.
        var category = await fixture.CategoryAsync();
        var created = await _client.PostAsJsonAsync("/api/items", new CreateItemRequest
        {
            Name = "QA Audit Stamp",
            AssetTag = $"QA-AUD-{TestData.Unique()}",
            ItemCategoryId = category.Id,
        });
        var item = (await created.Content.ReadFromJsonAsync<ItemResponse>(Json))!;

        await using var verify = fixture.CreateContext();
        var stored = await verify.Items.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        stored.CreatedBy.Should().Be("staff", "the stub principal populates created_by");
        stored.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
    }

    // --- Problem-details helper --------------------------------------------

    private sealed record Problem(string? Title, int? Status, string? Detail, string? Instance, string? TraceId);

    private static async Task<Problem> ReadProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        string? Read(string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        int? status = root.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.Number
            ? s.GetInt32()
            : null;

        return new Problem(Read("title"), status, Read("detail"), Read("instance"), Read("traceId"));
    }
}
