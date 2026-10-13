using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class KeepWhetherARequestsReceiptWasSent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "receipt_sent_at",
            schema: "identity",
            table: "privacy_requests",
            type: "timestamp with time zone",
            nullable: true);

        // PRIV-RIGHT-002 AC1 (D-186): a request queued before a refused receipt left it
        // standing was queued only with its receipt, at creation, which is what it read
        // back as and goes on reading back as.
        migrationBuilder.Sql(
            """
            UPDATE identity.privacy_requests
            SET receipt_sent_at = created_at;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "receipt_sent_at",
            schema: "identity",
            table: "privacy_requests");
    }
}
