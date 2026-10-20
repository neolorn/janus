using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class KeepTheCookiesANoticeLists : Migration
{
    private static readonly string[] VersionKey = ["document", "version"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "legal_document_cookies",
            schema: "identity",
            columns: table => new
            {
                document = table.Column<string>(type: "text", nullable: false),
                version = table.Column<string>(type: "text", nullable: false),
                cookie = table.Column<string>(type: "text", nullable: false),
                purpose = table.Column<string>(type: "text", nullable: true),
                lifetime = table.Column<TimeSpan>(type: "interval", nullable: true),
                third_party = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_legal_document_cookies", x => new { x.document, x.version, x.cookie });
                table.CheckConstraint("ck_legal_document_cookies_cookie", "length(trim(cookie)) > 0");
                table.CheckConstraint("ck_legal_document_cookies_lifetime", "lifetime IS NULL OR lifetime > interval '0'");
                table.CheckConstraint("ck_legal_document_cookies_purpose", "purpose IS NULL OR length(trim(purpose)) > 0");
                table.ForeignKey(
                    name: "fk_legal_document_cookies_version",
                    columns: x => new { x.document, x.version },
                    principalSchema: "identity",
                    principalTable: "legal_document_versions",
                    principalColumns: VersionKey,
                    onDelete: ReferentialAction.Cascade);
            });

        // OPS-MIG-003: the application keeps the cookies with each version of the notice
        // it publishes and reads them back with it.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.legal_document_cookies TO identity_app;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "legal_document_cookies",
            schema: "identity");
    }
}
