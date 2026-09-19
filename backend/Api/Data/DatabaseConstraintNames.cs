namespace Api.Data;

/// <summary>
/// Physical constraint names the service layer needs to recognise, so nobody has to
/// hard-code a string when translating a Postgres error into a domain result.
/// </summary>
/// <remarks>
/// A unique-violation surfaces as <c>Npgsql.PostgresException</c> with
/// <c>SqlState == "23505"</c> and <c>ConstraintName</c> set to one of these.
/// </remarks>
public static class DatabaseConstraintNames
{
    /// <summary>
    /// Filtered unique index on <c>loans (item_id) WHERE returned_at IS NULL</c>.
    /// A violation means the item is already out on loan — the BR-1 blocked path.
    /// </summary>
    public const string OpenLoanPerItem = "ux_loans_item_open";

    /// <summary>Unique index on <c>items (asset_tag)</c>.</summary>
    public const string ItemAssetTag = "ux_items_asset_tag";

    /// <summary>Unique index on <c>item_categories (name)</c>.</summary>
    public const string ItemCategoryName = "ux_item_categories_name";

    /// <summary>Unique index on <c>loan_statuses (name)</c>.</summary>
    public const string LoanStatusName = "ux_loan_statuses_name";
}
