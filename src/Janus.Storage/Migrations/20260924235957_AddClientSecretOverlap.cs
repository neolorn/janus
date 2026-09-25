using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddClientSecretOverlap : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "previous_secret",
            schema: "identity",
            table: "oidc_clients",
            type: "bytea",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "previous_secret_until",
            schema: "identity",
            table: "oidc_clients",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_oidc_clients_previous",
            schema: "identity",
            table: "oidc_clients",
            sql: "(previous_secret IS NULL) = (previous_secret_until IS NULL)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_oidc_clients_previous",
            schema: "identity",
            table: "oidc_clients");

        migrationBuilder.DropColumn(
            name: "previous_secret",
            schema: "identity",
            table: "oidc_clients");

        migrationBuilder.DropColumn(
            name: "previous_secret_until",
            schema: "identity",
            table: "oidc_clients");
    }
}
