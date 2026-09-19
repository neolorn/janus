using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddOrganizations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:CollationDefinition:public.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False")
            .OldAnnotation("Npgsql:CollationDefinition:janus.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False");

        migrationBuilder.CreateTable(
            name: "organizations",
            schema: "janus",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false, collation: "janus_ci"),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                deletion_requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                erased_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_organizations", x => x.id);
                table.CheckConstraint("ck_organizations_erased", "erased_at IS NULL OR deletion_requested_at IS NOT NULL");
            });

        migrationBuilder.CreateTable(
            name: "memberships",
            schema: "janus",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                organization = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_memberships", x => x.id);
                table.CheckConstraint("ck_memberships_ended", "ended_at IS NULL OR ended_at >= created_at");
                table.ForeignKey(
                    name: "fk_memberships_organization",
                    column: x => x.organization,
                    principalSchema: "janus",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_memberships_subject",
                    column: x => x.subject,
                    principalSchema: "janus",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_memberships_organization",
            schema: "janus",
            table: "memberships",
            column: "organization");

        migrationBuilder.CreateIndex(
            name: "ix_memberships_subject",
            schema: "janus",
            table: "memberships",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ix_organizations_deletion_requested_at",
            schema: "janus",
            table: "organizations",
            column: "deletion_requested_at",
            filter: "deletion_requested_at IS NOT NULL AND erased_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "memberships",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "organizations",
            schema: "janus");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:CollationDefinition:janus.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False")
            .OldAnnotation("Npgsql:CollationDefinition:public.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False");
    }
}
