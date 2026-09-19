using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Data.Seed;

/// <summary>
/// Idempotent local-development seed. Reference data (loan statuses, item categories) is
/// required for the product to function; the sample items and borrowers exist so the
/// checkout wizard and the list screens have something to show on a fresh local database.
/// </summary>
/// <remarks>
/// <para>All seeded data is synthetic — no real person, no real asset, no real contact detail.</para>
/// <para>
/// Idempotent by natural key (status/category name, item asset tag, borrower name): running
/// it repeatedly inserts nothing the second time. Ids are fixed constants so a re-seeded
/// local database keeps stable identifiers for E2E fixtures.
/// </para>
/// <para>
/// This does NOT create or migrate the database — migrations are generated only, and applied
/// by the operator. Call it after the schema exists.
/// </para>
/// <para>Inserts are batched with <c>AddRange</c>, never row-by-row in a loop.</para>
/// </remarks>
public static class SeedData
{
    /// <summary>Audit stamp on every seeded row.</summary>
    public const string SeedActor = "seed";

    public static class StatusIds
    {
        public static readonly Guid CheckedOut = new("6f1a0001-0000-4000-8000-000000000001");
        public static readonly Guid Returned = new("6f1a0001-0000-4000-8000-000000000002");
        public static readonly Guid Lost = new("6f1a0001-0000-4000-8000-000000000003");
        public static readonly Guid Damaged = new("6f1a0001-0000-4000-8000-000000000004");
    }

    public static class CategoryIds
    {
        public static readonly Guid PowerTools = new("6f1a0002-0000-4000-8000-000000000001");
        public static readonly Guid AvEquipment = new("6f1a0002-0000-4000-8000-000000000002");
        public static readonly Guid LabEquipment = new("6f1a0002-0000-4000-8000-000000000003");
    }

    /// <summary>
    /// Inserts any missing reference and sample rows. Safe to call on every start-up.
    /// </summary>
    /// <returns>The number of rows written (0 when everything already existed).</returns>
    public static async Task<int> SeedAsync(
        AppDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var written = 0;

        written += await SeedLoanStatusesAsync(db, cancellationToken);
        written += await SeedItemCategoriesAsync(db, cancellationToken);
        written += await SeedBorrowersAsync(db, cancellationToken);
        written += await SeedItemsAsync(db, cancellationToken);

        if (written > 0)
        {
            logger?.LogInformation("Seed complete: {RowCount} row(s) inserted.", written);
        }
        else
        {
            logger?.LogInformation("Seed complete: nothing to insert, data already present.");
        }

        return written;
    }

    // ---------------------------------------------------------------------
    // Reference data — required for the product to function.
    // ---------------------------------------------------------------------

    private static async Task<int> SeedLoanStatusesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.LoanStatuses
            .AsNoTracking()
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);

        var present = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = new[]
        {
            NewStatus(StatusIds.CheckedOut, "Checked Out", "Loan is open", isTerminal: false),
            NewStatus(StatusIds.Returned, "Returned", "Returned in good condition", isTerminal: true),
            NewStatus(StatusIds.Lost, "Lost", "Item was not returned", isTerminal: true),
            NewStatus(StatusIds.Damaged, "Damaged", "Returned but damaged", isTerminal: true)
        };

        var missing = candidates.Where(s => !present.Contains(s.Name)).ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        db.LoanStatuses.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }

    private static async Task<int> SeedItemCategoriesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.ItemCategories
            .AsNoTracking()
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        var present = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = new[]
        {
            NewCategory(CategoryIds.PowerTools, "Power Tools", "Hand-held and bench power tools"),
            NewCategory(CategoryIds.AvEquipment, "AV Equipment", "Cameras, projectors, audio and video gear"),
            NewCategory(CategoryIds.LabEquipment, "Lab Equipment", "Bench instruments and measurement devices")
        };

        var missing = candidates.Where(c => !present.Contains(c.Name)).ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        db.ItemCategories.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }

    // ---------------------------------------------------------------------
    // Sample data — synthetic, local development convenience only.
    // ---------------------------------------------------------------------

    private static async Task<int> SeedBorrowersAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Borrowers
            .AsNoTracking()
            .Select(b => b.Name)
            .ToListAsync(cancellationToken);

        var present = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = new[]
        {
            NewBorrower("Avery Nakamura", "avery.nakamura@example.invalid", "555-0101", "Facilities"),
            NewBorrower("Jordan Okafor", "jordan.okafor@example.invalid", "555-0102", "Media Services"),
            NewBorrower("Rowan Delgado", "rowan.delgado@example.invalid", "555-0103", "Research Lab"),
            NewBorrower("Sasha Lindqvist", "sasha.lindqvist@example.invalid", "555-0104", "Facilities")
        };

        var missing = candidates.Where(b => !present.Contains(b.Name)).ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        db.Borrowers.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }

    private static async Task<int> SeedItemsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        // Resolve category ids from the database rather than assuming the constants —
        // the categories may pre-date this seed run with different ids.
        var categoryIdsByName = await db.ItemCategories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existing = await db.Items
            .AsNoTracking()
            .Select(i => i.AssetTag)
            .ToListAsync(cancellationToken);

        var present = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<Item>();

        void Add(string categoryName, string assetTag, string name, string description)
        {
            if (!categoryIdsByName.TryGetValue(categoryName, out var categoryId))
            {
                return;
            }

            candidates.Add(NewItem(categoryId, assetTag, name, description));
        }

        Add("Power Tools", "PT-1001", "Cordless Drill", "18V cordless drill with two batteries");
        Add("Power Tools", "PT-1002", "Circular Saw", "184mm circular saw, corded");
        Add("Power Tools", "PT-1003", "Impact Driver", "18V impact driver with bit set");
        Add("AV Equipment", "AV-2001", "Projector", "1080p portable projector with HDMI");
        Add("AV Equipment", "AV-2002", "Field Recorder", "4-channel portable audio recorder");
        Add("AV Equipment", "AV-2003", "Tripod", "Fluid-head video tripod");
        Add("Lab Equipment", "LB-3001", "Digital Multimeter", "Bench multimeter, 6.5 digit");
        Add("Lab Equipment", "LB-3002", "Oscilloscope", "100MHz 4-channel oscilloscope");

        var missing = candidates.Where(i => !present.Contains(i.AssetTag)).ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        db.Items.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }

    // ---------------------------------------------------------------------
    // Factories — every seeded row carries the same audit stamp.
    // ---------------------------------------------------------------------

    private static LoanStatus NewStatus(Guid id, string name, string description, bool isTerminal) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        IsTerminal = isTerminal,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = SeedActor
    };

    private static ItemCategory NewCategory(Guid id, string name, string description) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = SeedActor
    };

    private static Borrower NewBorrower(string name, string email, string phone, string department) => new()
    {
        Name = name,
        ContactEmail = email,
        ContactPhone = phone,
        Department = department,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = SeedActor
    };

    private static Item NewItem(Guid categoryId, string assetTag, string name, string description) => new()
    {
        ItemCategoryId = categoryId,
        AssetTag = assetTag,
        Name = name,
        Description = description,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = SeedActor
    };
}
