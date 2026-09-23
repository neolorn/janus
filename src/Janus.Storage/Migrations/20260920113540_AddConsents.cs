using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddConsents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "consents",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                purpose = table.Column<string>(type: "text", nullable: false),
                notice_version = table.Column<string>(type: "text", nullable: false),
                mechanism = table.Column<string>(type: "text", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_consents", x => new { x.subject, x.purpose });
                table.CheckConstraint("ck_consents_kind", "kind IN ('ordinary', 'written')");
                table.CheckConstraint("ck_consents_mechanism", "mechanism IN ('administrator', 'dashboard', 'reconsent', 'registration')");
                table.CheckConstraint("ck_consents_purpose", "length(trim(purpose)) > 0");
                table.ForeignKey(
                    name: "fk_consents_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "objections",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                purpose = table.Column<string>(type: "text", nullable: false),
                notice_version = table.Column<string>(type: "text", nullable: false),
                mechanism = table.Column<string>(type: "text", nullable: false),
                recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_objections", x => new { x.subject, x.purpose });
                table.CheckConstraint("ck_objections_mechanism", "mechanism IN ('administrator', 'dashboard', 'reconsent', 'registration')");
                table.CheckConstraint("ck_objections_purpose", "length(trim(purpose)) > 0");
                table.ForeignKey(
                    name: "fk_objections_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_consents_live",
            schema: "identity",
            table: "consents",
            column: "notice_version",
            filter: "withdrawn_at IS NULL AND superseded_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "consents",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "objections",
            schema: "identity");
    }
}
