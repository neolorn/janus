using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class DropTheSmsBalanceReadings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // D-225: a gateway's balance is the host's messaging's, so the library reads and
        // keeps none.
        migrationBuilder.DropTable(
            name: "sms_balance_readings",
            schema: "identity");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "sms_balance_readings",
            schema: "identity",
            columns: table => new
            {
                read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                balance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_sms_balance_readings", x => x.read_at));
    }
}
