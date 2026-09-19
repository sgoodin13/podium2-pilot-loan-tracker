using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "borrowers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    department = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_borrowers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "item_categories",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loan_statuses",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loan_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    asset_tag = table.Column<string>(type: "text", nullable: false),
                    item_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_items_item_categories_item_category_id",
                        column: x => x.item_category_id,
                        principalSchema: "public",
                        principalTable: "item_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loans",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrower_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checked_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loans", x => x.id);
                    table.ForeignKey(
                        name: "fk_loans_borrowers_borrower_id",
                        column: x => x.borrower_id,
                        principalSchema: "public",
                        principalTable: "borrowers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loans_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loans_loan_statuses_loan_status_id",
                        column: x => x.loan_status_id,
                        principalSchema: "public",
                        principalTable: "loan_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_item_categories_name",
                schema: "public",
                table: "item_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_items_item_category_id",
                schema: "public",
                table: "items",
                column: "item_category_id");

            migrationBuilder.CreateIndex(
                name: "ux_items_asset_tag",
                schema: "public",
                table: "items",
                column: "asset_tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_loan_statuses_name",
                schema: "public",
                table: "loan_statuses",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_loans_borrower_id",
                schema: "public",
                table: "loans",
                column: "borrower_id");

            migrationBuilder.CreateIndex(
                name: "idx_loans_item_id",
                schema: "public",
                table: "loans",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "idx_loans_loan_status_id",
                schema: "public",
                table: "loans",
                column: "loan_status_id");

            migrationBuilder.CreateIndex(
                name: "idx_loans_returned_at",
                schema: "public",
                table: "loans",
                column: "returned_at");

            migrationBuilder.CreateIndex(
                name: "ux_loans_item_open",
                schema: "public",
                table: "loans",
                column: "item_id",
                unique: true,
                filter: "returned_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loans",
                schema: "public");

            migrationBuilder.DropTable(
                name: "borrowers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "loan_statuses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "item_categories",
                schema: "public");
        }
    }
}
