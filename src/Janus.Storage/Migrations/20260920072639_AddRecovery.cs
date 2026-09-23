using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddRecovery : Migration
{
    private static readonly string[] ApproverAndInstant = ["approver", "approved_at"];
    private static readonly string[] SubjectAndPurpose = ["subject", "purpose"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "enrolment",
            schema: "identity",
            table: "preauthentication_sessions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "change_required",
            schema: "identity",
            table: "passwords",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "loss_reports",
            schema: "identity",
            columns: table => new
            {
                credential = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                enc_cancel = table.Column<byte[]>(type: "bytea", nullable: false),
                reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                invalidates_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                any_delivered = table.Column<bool>(type: "boolean", nullable: false),
                held_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_loss_reports", x => x.credential);
                table.CheckConstraint("ck_loss_reports_window", "invalidates_at > reported_at");
                table.ForeignKey(
                    name: "fk_loss_reports_credential",
                    column: x => x.credential,
                    principalSchema: "identity",
                    principalTable: "authenticators",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_loss_reports_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "recovery_approvals",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                approver = table.Column<Guid>(type: "uuid", nullable: false),
                approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                enc_channel = table.Column<byte[]>(type: "bytea", nullable: false),
                spent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_recovery_approvals", x => new { x.subject, x.approver, x.approved_at });
                table.ForeignKey(
                    name: "fk_recovery_approvals_approver",
                    column: x => x.approver,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_recovery_approvals_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "recovery_links",
            schema: "identity",
            columns: table => new
            {
                token = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                purpose = table.Column<string>(type: "text", nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                approver = table.Column<Guid>(type: "uuid", nullable: true),
                mailbox_lost = table.Column<bool>(type: "boolean", nullable: false),
                session = table.Column<Guid>(type: "uuid", nullable: true),
                spent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_recovery_links", x => x.token);
                table.CheckConstraint("ck_recovery_links_expiry", "expires_at > issued_at");
                table.CheckConstraint("ck_recovery_links_purpose", "purpose IN ('enrolment', 'self-service')");
                table.CheckConstraint("ck_recovery_links_session", "session IS NULL OR spent_at IS NOT NULL");
                table.CheckConstraint("ck_recovery_links_token", "octet_length(token) = 32");
                table.ForeignKey(
                    name: "fk_recovery_links_approver",
                    column: x => x.approver,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_recovery_links_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_preauthentication_sessions_enrolment",
            schema: "identity",
            table: "preauthentication_sessions",
            column: "enrolment",
            unique: true,
            filter: "enrolment IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_loss_reports_invalidates_at",
            schema: "identity",
            table: "loss_reports",
            column: "invalidates_at");

        migrationBuilder.CreateIndex(
            name: "ix_loss_reports_notified_at",
            schema: "identity",
            table: "loss_reports",
            column: "notified_at");

        migrationBuilder.CreateIndex(
            name: "ix_loss_reports_subject",
            schema: "identity",
            table: "loss_reports",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ix_recovery_approvals_approver",
            schema: "identity",
            table: "recovery_approvals",
            columns: ApproverAndInstant);

        migrationBuilder.CreateIndex(
            name: "ix_recovery_links_approver",
            schema: "identity",
            table: "recovery_links",
            column: "approver");

        migrationBuilder.CreateIndex(
            name: "ix_recovery_links_expires_at",
            schema: "identity",
            table: "recovery_links",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ux_recovery_links_session",
            schema: "identity",
            table: "recovery_links",
            column: "session",
            unique: true,
            filter: "session IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ux_recovery_links_subject_purpose",
            schema: "identity",
            table: "recovery_links",
            columns: SubjectAndPurpose,
            unique: true,
            filter: "spent_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "loss_reports",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "recovery_approvals",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "recovery_links",
            schema: "identity");

        migrationBuilder.DropIndex(
            name: "ux_preauthentication_sessions_enrolment",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropColumn(
            name: "enrolment",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropColumn(
            name: "change_required",
            schema: "identity",
            table: "passwords");
    }
}
