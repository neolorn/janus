using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class ProvisionMailboxes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "mailboxes",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                fingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                canonicalisation_version = table.Column<string>(type: "text", nullable: false),
                enc_canonical = table.Column<byte[]>(type: "bytea", nullable: false),
                key_version = table.Column<int>(type: "integer", nullable: true),
                wrapped_key = table.Column<byte[]>(type: "bytea", nullable: true),
                reserved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                holder = table.Column<Guid>(type: "uuid", nullable: true),
                retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                pushed = table.Column<string>(type: "text", nullable: true),
                pending = table.Column<string>(type: "text", nullable: true),
                pending_key = table.Column<Guid>(type: "uuid", nullable: true),
                attempts = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_mailboxes", x => x.id);
                table.CheckConstraint("ck_mailboxes_key", "(holder IS NULL) = (wrapped_key IS NOT NULL) AND (wrapped_key IS NULL) = (key_version IS NULL)");
                table.CheckConstraint("ck_mailboxes_pending", "pending IS NULL OR pending IN ('disabled', 'enabled', 'removed')");
                table.CheckConstraint("ck_mailboxes_pending_key", "(pending IS NULL) = (pending_key IS NULL)");
                table.CheckConstraint("ck_mailboxes_pushed", "pushed IS NULL OR pushed IN ('disabled', 'enabled', 'removed')");
                table.CheckConstraint("ck_mailboxes_released", "released_at IS NULL OR (holder IS NULL AND retired_at IS NULL)");
                table.ForeignKey(
                    name: "fk_mailboxes_holder",
                    column: x => x.holder,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_mailboxes_holder",
            schema: "identity",
            table: "mailboxes",
            column: "holder");

        migrationBuilder.CreateIndex(
            name: "ux_mailboxes_fingerprint",
            schema: "identity",
            table: "mailboxes",
            column: "fingerprint",
            unique: true,
            filter: "fingerprint <> decode(repeat('00', 32), 'hex')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "mailboxes",
            schema: "identity");
    }
}
