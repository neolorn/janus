using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddInvitations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "invitations",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization = table.Column<Guid>(type: "uuid", nullable: false),
                inviter = table.Column<Guid>(type: "uuid", nullable: false),
                token = table.Column<byte[]>(type: "bytea", nullable: false),
                key_version = table.Column<int>(type: "integer", nullable: true),
                wrapped_key = table.Column<byte[]>(type: "bytea", nullable: true),
                enc_identifiers = table.Column<byte[]>(type: "bytea", nullable: true),
                roles = table.Column<string[]>(type: "text[]", nullable: false),
                documents = table.Column<string>(type: "jsonb", nullable: false),
                mailbox = table.Column<Guid>(type: "uuid", nullable: true),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                session = table.Column<Guid>(type: "uuid", nullable: true),
                invitee = table.Column<Guid>(type: "uuid", nullable: true),
                attached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_invitations", x => x.id);
                table.CheckConstraint("ck_invitations_attached", "session IS NULL OR invitee IS NULL");
                table.CheckConstraint("ck_invitations_forgotten", "enc_identifiers IS NULL OR (revoked_at IS NULL AND acknowledged_at IS NULL)");
                table.CheckConstraint("ck_invitations_key", "(enc_identifiers IS NULL) = (wrapped_key IS NULL) AND (wrapped_key IS NULL) = (key_version IS NULL)");
                table.CheckConstraint("ck_invitations_outcome", "revoked_at IS NULL OR acknowledged_at IS NULL");
                table.ForeignKey(
                    name: "fk_invitations_invitee",
                    column: x => x.invitee,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_invitations_inviter",
                    column: x => x.inviter,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_invitations_mailbox",
                    column: x => x.mailbox,
                    principalSchema: "identity",
                    principalTable: "mailboxes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_invitations_organization",
                    column: x => x.organization,
                    principalSchema: "identity",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_invitations_invitee",
            schema: "identity",
            table: "invitations",
            column: "invitee");

        migrationBuilder.CreateIndex(
            name: "ix_invitations_inviter",
            schema: "identity",
            table: "invitations",
            column: "inviter");

        migrationBuilder.CreateIndex(
            name: "ix_invitations_organization",
            schema: "identity",
            table: "invitations",
            column: "organization");

        migrationBuilder.CreateIndex(
            name: "ux_invitations_mailbox",
            schema: "identity",
            table: "invitations",
            column: "mailbox",
            unique: true,
            filter: "mailbox IS NOT NULL AND revoked_at IS NULL AND acknowledged_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ux_invitations_token",
            schema: "identity",
            table: "invitations",
            column: "token",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "invitations",
            schema: "identity");
    }
}
