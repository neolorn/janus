using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class BindATextedSecondStepToItsNumber : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTH-FACT-002 (D-197): a credential held before this runs is bound to no
        // number, so no code is texted for it.
        migrationBuilder.AddColumn<byte[]>(
            name: "enc_number",
            schema: "identity",
            table: "authenticators",
            type: "bytea",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_authenticators_number",
            schema: "identity",
            table: "authenticators",
            sql: "(enc_number IS NULL) OR (factor IN ('phoneCode'))");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_authenticators_number",
            schema: "identity",
            table: "authenticators");

        migrationBuilder.DropColumn(
            name: "enc_number",
            schema: "identity",
            table: "authenticators");
    }
}
