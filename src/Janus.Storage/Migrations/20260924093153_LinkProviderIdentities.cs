using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class LinkProviderIdentities : Migration
{
    private static readonly string[] FactorAndProviderSubject = ["factor", "provider_subject"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "provider_subject",
            schema: "identity",
            table: "authenticators",
            type: "bytea",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ux_authenticators_provider_subject",
            schema: "identity",
            table: "authenticators",
            columns: FactorAndProviderSubject,
            unique: true,
            filter: "provider_subject IS NOT NULL AND provider_subject <> decode(repeat('00', 32), 'hex')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_authenticators_provider_subject",
            schema: "identity",
            table: "authenticators",
            sql: "(provider_subject IS NULL) <> (factor IN ('apple', 'google')) AND (provider_subject IS NULL OR octet_length(provider_subject) = 32)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_authenticators_provider_subject",
            schema: "identity",
            table: "authenticators");

        migrationBuilder.DropCheckConstraint(
            name: "ck_authenticators_provider_subject",
            schema: "identity",
            table: "authenticators");

        migrationBuilder.DropColumn(
            name: "provider_subject",
            schema: "identity",
            table: "authenticators");
    }
}
