using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class NameTheInvitationAMembershipBeganFrom : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // IDN-READ-002 (D-218): a membership bootstrap attached began from no invitation.
        migrationBuilder.AddColumn<Guid>(
            name: "invitation",
            schema: "identity",
            table: "memberships",
            type: "uuid",
            nullable: true);

        // An acknowledgement attaches the membership and marks its invitation at the one
        // instant, for the account the invitation attached to, so a membership standing
        // before this migration is matched to the invitation it began from.
        migrationBuilder.Sql(
            """
            UPDATE identity.memberships AS membership
            SET invitation = invitation.id
            FROM identity.invitations AS invitation
            WHERE invitation.invitee = membership.subject
                AND invitation.organization = membership.organization
                AND invitation.acknowledged_at = membership.acknowledged_at;
            """);

        migrationBuilder.CreateIndex(
            name: "ix_memberships_invitation",
            schema: "identity",
            table: "memberships",
            column: "invitation");

        migrationBuilder.AddForeignKey(
            name: "fk_memberships_invitation",
            schema: "identity",
            table: "memberships",
            column: "invitation",
            principalSchema: "identity",
            principalTable: "invitations",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropForeignKey(
            name: "fk_memberships_invitation",
            schema: "identity",
            table: "memberships");

        migrationBuilder.DropIndex(
            name: "ix_memberships_invitation",
            schema: "identity",
            table: "memberships");

        migrationBuilder.DropColumn(
            name: "invitation",
            schema: "identity",
            table: "memberships");
    }
}
