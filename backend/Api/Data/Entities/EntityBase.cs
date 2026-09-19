namespace Api.Data.Entities;

/// <summary>
/// Shared base for every LoanTracker entity.
/// Carries the Gate 1 rulings that apply to every table: a uuid surrogate key,
/// the soft-delete flag, and the four audit columns.
/// </summary>
/// <remarks>
/// There is no identity store to foreign-key against (auth is a stub for this pilot),
/// so <see cref="CreatedBy"/> / <see cref="ModifiedBy"/> are plain text labels.
/// </remarks>
public abstract class EntityBase
{
    /// <summary>Surrogate primary key. Postgres default <c>gen_random_uuid()</c>.</summary>
    public Guid Id { get; set; }

    /// <summary>Soft-delete flag. False means logically deleted; rows are never physically removed.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Who created the row. Plain text — no identity store to reference.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>When the row was last modified (UTC). Null until the first modification.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>Who last modified the row. Null until the first modification.</summary>
    public string? ModifiedBy { get; set; }
}
