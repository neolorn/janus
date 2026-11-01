using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
/// <remarks>
/// Chapter 10 section 5a (D-235): the gates a stored policy or override still names for
/// a retired step-up action are deleted, so a later release need not keep reading past
/// them. Only the system policy and the organizations' overrides hold gates; the other
/// keys under the prefix are not read as JSON.
/// </remarks>
internal sealed partial class RemoveTheGatesOfARetiredAction : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            """
            UPDATE identity.settings AS setting
            SET value = jsonb_set(policy.value, '{gates}', (policy.value -> 'gates') - 'alerting:destinations')::text
            FROM (
                SELECT key, value::jsonb AS value
                FROM identity.settings
                WHERE key = 'policy.default'
                    OR key ~ '^policy\.[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                OFFSET 0
            ) AS policy
            WHERE setting.key = policy.key
                AND jsonb_typeof(policy.value -> 'gates') = 'object'
                AND policy.value -> 'gates' ? 'alerting:destinations';
            """);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The version before reads a policy whose gates name current actions only, so the
    /// deleted gates are not put back.
    /// </remarks>
    protected override void Down(MigrationBuilder migrationBuilder) =>
        ArgumentNullException.ThrowIfNull(migrationBuilder);
}
