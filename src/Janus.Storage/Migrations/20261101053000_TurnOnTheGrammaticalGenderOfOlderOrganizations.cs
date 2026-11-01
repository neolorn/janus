using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
/// <remarks>
/// OPS-MIG-001 (D-239): an organization created before the grammatical gender existed
/// holds an override written without the field, and so would inherit the system policy's
/// <c>false</c>; every organization now starts with it on, so the field is set
/// <c>true</c> in each override that lacks it. An override that states it, and the
/// system policy, are left as they are.
/// </remarks>
internal sealed partial class TurnOnTheGrammaticalGenderOfOlderOrganizations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            """
            UPDATE identity.settings AS setting
            SET value = jsonb_set(policy.value, '{grammaticalGender}', 'true'::jsonb)::text
            FROM (
                SELECT key, value::jsonb AS value
                FROM identity.settings
                WHERE key ~ '^policy\.[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                OFFSET 0
            ) AS policy
            WHERE setting.key = policy.key
                AND jsonb_typeof(policy.value) = 'object'
                AND NOT policy.value ? 'grammaticalGender';
            """);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The version before reads the field as any override's, so the overrides it turned
    /// on are left on.
    /// </remarks>
    protected override void Down(MigrationBuilder migrationBuilder) =>
        ArgumentNullException.ThrowIfNull(migrationBuilder);
}
