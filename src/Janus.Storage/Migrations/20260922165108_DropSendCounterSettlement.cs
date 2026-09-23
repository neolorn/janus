using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class DropSendCounterSettlement : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_send_counters_settles_at",
            schema: "janus",
            table: "send_counters");

        migrationBuilder.DropColumn(
            name: "settles_at",
            schema: "janus",
            table: "send_counters");

        // AUTH-ABUSE-004 AC6: the sweep before a read deletes every record whose newest
        // time decides nothing, and the times are written oldest first, so what it reads
        // is the last element of the array. No model builder expresses an index over an
        // expression, and the planner matches one only on the expression as written, so
        // this is the statement the sweep generates, element for element.
        migrationBuilder.Sql(
            """
            CREATE INDEX ix_send_counters_last_sent_at
                ON janus.send_counters ((sent_at[(cardinality(sent_at) - 1) + 1]));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX janus.ix_send_counters_last_sent_at;");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "settles_at",
            schema: "janus",
            table: "send_counters",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

        migrationBuilder.CreateIndex(
            name: "ix_send_counters_settles_at",
            schema: "janus",
            table: "send_counters",
            column: "settles_at");
    }
}
