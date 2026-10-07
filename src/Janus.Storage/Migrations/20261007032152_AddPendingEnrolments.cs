using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddPendingEnrolments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "pending_enrolments",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                factor = table.Column<string>(type: "text", nullable: false),
                label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                enc_number = table.Column<byte[]>(type: "bytea", nullable: false),
                send = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                enc_code = table.Column<byte[]>(type: "bytea", nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                wrong_attempts = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pending_enrolments", x => new { x.subject, x.factor });
                table.CheckConstraint("ck_pending_enrolments_factor", "factor IN ('phoneCode')");
                table.CheckConstraint("ck_pending_enrolments_send", "octet_length(send) = 32");
                table.CheckConstraint("ck_pending_enrolments_subject_not_max_uuid", "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");
                table.CheckConstraint("ck_pending_enrolments_wrong_attempts", "wrong_attempts >= 0");
                table.ForeignKey(
                    name: "fk_pending_enrolments_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_pending_enrolments_expires_at",
            schema: "identity",
            table: "pending_enrolments",
            column: "expires_at");

        // OPS-MIG-003: the application stages, replaces, confirms and ends the
        // enrolments.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.pending_enrolments TO identity_app;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "pending_enrolments",
            schema: "identity");
    }
}
