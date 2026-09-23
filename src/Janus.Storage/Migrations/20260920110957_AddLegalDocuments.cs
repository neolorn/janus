using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddLegalDocuments : Migration
{
    private static readonly string[] VersionKey = ["document", "version"];

    private static readonly string[] CurrentIndex = ["document", "published_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "legal_document_versions",
            schema: "identity",
            columns: table => new
            {
                document = table.Column<string>(type: "text", nullable: false),
                version = table.Column<string>(type: "text", nullable: false),
                governing_language = table.Column<string>(type: "text", nullable: false),
                governing_text = table.Column<string>(type: "text", nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_legal_document_versions", x => new { x.document, x.version });
                table.CheckConstraint("ck_legal_document_versions_governing_language", "length(trim(governing_language)) > 0");
                table.CheckConstraint("ck_legal_document_versions_governing_text", "length(trim(governing_text)) > 0");
            });

        migrationBuilder.CreateTable(
            name: "legal_document_translations",
            schema: "identity",
            columns: table => new
            {
                document = table.Column<string>(type: "text", nullable: false),
                version = table.Column<string>(type: "text", nullable: false),
                language = table.Column<string>(type: "text", nullable: false),
                translated_text = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_legal_document_translations", x => new { x.document, x.version, x.language });
                table.CheckConstraint("ck_legal_document_translations_language", "length(trim(language)) > 0");
                table.ForeignKey(
                    name: "fk_legal_document_translations_version",
                    columns: x => new { x.document, x.version },
                    principalSchema: "identity",
                    principalTable: "legal_document_versions",
                    principalColumns: VersionKey,
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_legal_document_versions_current",
            schema: "identity",
            table: "legal_document_versions",
            columns: CurrentIndex);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "legal_document_translations",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "legal_document_versions",
            schema: "identity");
    }
}
