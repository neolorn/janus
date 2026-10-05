using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class NameTheCredentialASecondStepCodeIsIssuedFor : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTH-FACT-004 (D-192): a second step's code pending when this runs names no
        // credential and could not be judged against one, so it is removed and the
        // person asks again; a code lives minutes. Every row that stays is of an entry
        // whose code names none.
        migrationBuilder.Sql(
            """
            DELETE FROM identity.signin_links WHERE factor IN ('phoneCode');
            """);

        migrationBuilder.AddColumn<Guid>(
            name: "credential",
            schema: "identity",
            table: "signin_links",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_signin_links_credential",
            schema: "identity",
            table: "signin_links",
            sql: "(credential IS NOT NULL) = (factor IN ('phoneCode'))");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_signin_links_credential",
            schema: "identity",
            table: "signin_links");

        migrationBuilder.DropColumn(
            name: "credential",
            schema: "identity",
            table: "signin_links");
    }
}
