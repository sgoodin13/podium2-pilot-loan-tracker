using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Data.Entities;
using Api.Dtos;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

/// <summary>
/// Performance coverage on the loan list endpoint — the one list that joins three
/// tables and derives open/closed state on every row.
/// </summary>
/// <remarks>
/// STATED THRESHOLD: a page of 25 loans out of a 500-loan table must come back in
/// under 1500 ms, measured over the in-process pipeline (no network hop). The number
/// is a regression tripwire for an N+1 or a missing index, not a production SLA —
/// at pilot volumes the observed time is far below it.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class LoanListPerformanceTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const int SeededLoanCount = 500;
    private const int ThresholdMilliseconds = 1500;

    private LoanTrackerApiFactory _factory = null!;
    private HttpClient _client = null!;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        _factory = new LoanTrackerApiFactory(fixture.ConnectionString);
        _client = _factory.CreateClient();
        await SeedRepresentativeVolumeAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// 500 synthetic loans, inserted in ONE batched SaveChanges rather than row by row.
    /// BR-1 permits at most one open loan per item, so every loan gets its own item and
    /// half of them are closed — which also exercises both branches of the state filter.
    /// </summary>
    private async Task SeedRepresentativeVolumeAsync()
    {
        await using var db = fixture.CreateContext();

        var existing = await db.Loans.CountAsync();
        if (existing >= SeededLoanCount)
        {
            return;
        }

        var category = await db.ItemCategories.AsNoTracking().FirstAsync();
        var checkedOut = await db.LoanStatuses.AsNoTracking().SingleAsync(s => s.Name == "Checked Out");
        var returned = await db.LoanStatuses.AsNoTracking().SingleAsync(s => s.Name == "Returned");

        var run = TestData.Unique();

        var borrowers = Enumerable.Range(0, 20)
            .Select(i => new Borrower
            {
                Name = $"QA Perf Borrower {run}-{i:D3}",
                Department = "QA Performance",
                IsActive = true,
            })
            .ToList();

        var items = Enumerable.Range(0, SeededLoanCount)
            .Select(i => new Item
            {
                Name = $"QA Perf Item {run}-{i:D4}",
                AssetTag = $"QAP-{run}-{i:D4}",
                ItemCategoryId = category.Id,
                IsActive = true,
            })
            .ToList();

        db.Borrowers.AddRange(borrowers);
        db.Items.AddRange(items);
        await db.SaveChangesAsync();

        var loans = items
            .Select((item, i) => new Loan
            {
                ItemId = item.Id,
                BorrowerId = borrowers[i % borrowers.Count].Id,
                LoanStatusId = i % 2 == 0 ? returned.Id : checkedOut.Id,
                CheckedOutAt = DateTimeOffset.UtcNow.AddDays(-i),
                ReturnedAt = i % 2 == 0 ? DateTimeOffset.UtcNow.AddDays(-i).AddHours(6) : null,
            })
            .ToList();

        db.Loans.AddRange(loans);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Loan_list_page_returns_within_1500ms_with_500_loans_seeded()
    {
        await using (var db = fixture.CreateContext())
        {
            (await db.Loans.CountAsync()).Should().BeGreaterThanOrEqualTo(SeededLoanCount);
        }

        // One untimed call so connection-pool warm-up is not charged to the measurement.
        (await _client.GetAsync("/api/loans?page=1&pageSize=25")).EnsureSuccessStatusCode();

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/api/loans?page=1&pageSize=25");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<LoanResponse>>(Json);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();
        page!.Items.Should().HaveCount(25, "the endpoint must page rather than return the whole table");
        page.TotalCount.Should().BeGreaterThanOrEqualTo(SeededLoanCount);

        // Every row carries its joined item/borrower/status — proof the page was
        // eager-loaded in one query rather than lazily filled per row.
        page.Items.Should().OnlyContain(l =>
            !string.IsNullOrWhiteSpace(l.ItemName)
            && !string.IsNullOrWhiteSpace(l.ItemAssetTag)
            && !string.IsNullOrWhiteSpace(l.BorrowerName)
            && !string.IsNullOrWhiteSpace(l.LoanStatusName));

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(
            ThresholdMilliseconds,
            "a page of 25 out of {0} loans must stay under the stated {1} ms threshold",
            SeededLoanCount,
            ThresholdMilliseconds);
    }

    [Fact]
    public async Task Filtered_loan_list_page_also_returns_within_the_threshold()
    {
        (await _client.GetAsync("/api/loans?state=open&page=1&pageSize=25")).EnsureSuccessStatusCode();

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/api/loans?state=open&page=1&pageSize=25");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<LoanResponse>>(Json);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();
        page!.Items.Should().OnlyContain(l => l.IsOpen);

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(ThresholdMilliseconds);
    }

    /// <summary>
    /// The item list derives availability for a whole page in one extra query. With 500
    /// items on loan this is where an N+1 would show up first.
    /// </summary>
    [Fact]
    public async Task Item_list_page_derives_availability_within_the_threshold()
    {
        (await _client.GetAsync("/api/items?page=1&pageSize=25")).EnsureSuccessStatusCode();

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/api/items?page=1&pageSize=25");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ItemResponse>>(Json);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();
        page!.Items.Should().HaveCount(25);

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(ThresholdMilliseconds);
    }
}
