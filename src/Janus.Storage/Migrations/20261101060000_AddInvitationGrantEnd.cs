using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
/// <remarks>
/// AUTHZ-GRANT-003 (D-239): an invitation naming a role whose grants must end keeps the
/// end it gives those grants, and the roles it applies to, as it was issued. An invitation
/// issued before holds neither and attaches as issued.
/// </remarks>
internal sealed partial class AddInvitationGrantEnd : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<string[]>(
            name: "grant_ending_roles",
            schema: "identity",
            table: "invitations",
            type: "text[]",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "grant_ends_at",
            schema: "identity",
            table: "invitations",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_invitations_grant_end",
            schema: "identity",
            table: "invitations",
            sql: "(grant_ends_at IS NULL) = (grant_ending_roles IS NULL)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_invitations_grant_end",
            schema: "identity",
            table: "invitations");

        migrationBuilder.DropColumn(
            name: "grant_ending_roles",
            schema: "identity",
            table: "invitations");

        migrationBuilder.DropColumn(
            name: "grant_ends_at",
            schema: "identity",
            table: "invitations");
    }
}
