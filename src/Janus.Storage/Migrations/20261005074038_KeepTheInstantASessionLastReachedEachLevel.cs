using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class KeepTheInstantASessionLastReachedEachLevel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTH-SESS-001 (D-191): the one instant a session kept was written by every
        // presentation, whatever it reached, so it is the instant the lowest level was
        // last reached.
        migrationBuilder.RenameColumn(
            name: "attained_at",
            schema: "identity",
            table: "sessions",
            newName: "delegated_at");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "aal1_at",
            schema: "identity",
            table: "sessions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "aal2_at",
            schema: "identity",
            table: "sessions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "aal3_at",
            schema: "identity",
            table: "sessions",
            type: "timestamp with time zone",
            nullable: true);

        // AUTH-SESS-001 (D-191): a session already recorded is carried over. Its instant
        // becomes the instant of each level up to the one it holds. The instant it last
        // reached phishing resistance is the column it already keeps, which stands.
        migrationBuilder.Sql(
            """
            UPDATE identity.sessions SET
                aal1_at = CASE WHEN attained IN ('aal1', 'aal2', 'aal3') THEN delegated_at END,
                aal2_at = CASE WHEN attained IN ('aal2', 'aal3') THEN delegated_at END,
                aal3_at = CASE WHEN attained = 'aal3' THEN delegated_at END;
            """);

        migrationBuilder.DropCheckConstraint(
            name: "ck_sessions_attained",
            schema: "identity",
            table: "sessions");

        migrationBuilder.DropCheckConstraint(
            name: "ck_sessions_phishing_resistant",
            schema: "identity",
            table: "sessions");

        migrationBuilder.DropColumn(
            name: "attained",
            schema: "identity",
            table: "sessions");

        migrationBuilder.DropColumn(
            name: "phishing_resistant",
            schema: "identity",
            table: "sessions");

        migrationBuilder.AddCheckConstraint(
            name: "ck_sessions_levels",
            schema: "identity",
            table: "sessions",
            sql: "(aal1_at IS NULL OR aal1_at <= delegated_at) AND (aal2_at IS NULL OR (aal1_at IS NOT NULL AND aal2_at <= aal1_at)) AND (aal3_at IS NULL OR (aal2_at IS NOT NULL AND aal3_at <= aal2_at))");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_sessions_levels",
            schema: "identity",
            table: "sessions");

        migrationBuilder.AddColumn<string>(
            name: "attained",
            schema: "identity",
            table: "sessions",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "phishing_resistant",
            schema: "identity",
            table: "sessions",
            type: "boolean",
            nullable: true);

        // AUTH-SESS-001: the earlier row holds one level, the highest the session has
        // reached, and whether it reached phishing resistance; the instant it keeps is
        // the instant of its last presentation, which the lowest level's is.
        migrationBuilder.Sql(
            """
            UPDATE identity.sessions SET
                attained = CASE
                    WHEN aal3_at IS NOT NULL THEN 'aal3'
                    WHEN aal2_at IS NOT NULL THEN 'aal2'
                    WHEN aal1_at IS NOT NULL THEN 'aal1'
                    ELSE 'delegated' END,
                phishing_resistant = (phishing_resistant_at IS NOT NULL);
            """);

        migrationBuilder.AlterColumn<string>(
            name: "attained",
            schema: "identity",
            table: "sessions",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<bool>(
            name: "phishing_resistant",
            schema: "identity",
            table: "sessions",
            type: "boolean",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "boolean",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "aal1_at",
            schema: "identity",
            table: "sessions");

        migrationBuilder.DropColumn(
            name: "aal2_at",
            schema: "identity",
            table: "sessions");

        migrationBuilder.DropColumn(
            name: "aal3_at",
            schema: "identity",
            table: "sessions");

        migrationBuilder.RenameColumn(
            name: "delegated_at",
            schema: "identity",
            table: "sessions",
            newName: "attained_at");

        migrationBuilder.AddCheckConstraint(
            name: "ck_sessions_attained",
            schema: "identity",
            table: "sessions",
            sql: "attained IN ('aal1', 'aal2', 'aal3', 'delegated')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_sessions_phishing_resistant",
            schema: "identity",
            table: "sessions",
            sql: "phishing_resistant = (phishing_resistant_at IS NOT NULL)");
    }
}
