using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class LockMembersToVerifiedDomains : Migration
{
    private static readonly string[] OrganizationAndDomain = ["organization", "domain"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<Guid>(
            name: "email",
            schema: "identity",
            table: "signin_links",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "email",
            schema: "identity",
            table: "signin_challenges",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "organization_domains",
            schema: "identity",
            columns: table => new
            {
                token = table.Column<string>(type: "text", nullable: false),
                organization = table.Column<Guid>(type: "uuid", nullable: false),
                domain = table.Column<string>(type: "text", nullable: false),
                added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_check_passed = table.Column<bool>(type: "boolean", nullable: true),
                removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_organization_domains", x => x.token);
                table.CheckConstraint("ck_organization_domains_verified", "verified_at IS NULL OR checked_at IS NOT NULL");
                table.ForeignKey(
                    name: "fk_organization_domains_organization",
                    column: x => x.organization,
                    principalSchema: "identity",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_organization_domains_organization_domain",
            schema: "identity",
            table: "organization_domains",
            columns: OrganizationAndDomain,
            unique: true,
            filter: "removed_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "organization_domains",
            schema: "identity");

        migrationBuilder.DropColumn(
            name: "email",
            schema: "identity",
            table: "signin_links");

        migrationBuilder.DropColumn(
            name: "email",
            schema: "identity",
            table: "signin_challenges");
    }
}
