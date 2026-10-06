using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class NarrowWhatAFingerprintRetirementDeletes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // OPS-SEC-003, OPS-MIG-003a AC4 (D-183): a fingerprint key's retirement waits for
        // every line that lapses on a clock of its own and deletes none of them, so the
        // maintenance role keeps the version of each abuse ledger line and of each
        // sign-in in progress to read, and deletes only unspent restriction credit and
        // released username holds.
        migrationBuilder.Sql(
            """
            REVOKE DELETE ON identity.callbacks FROM identity_maintenance;
            REVOKE DELETE ON identity.nonexistence_notices FROM identity_maintenance;
            REVOKE DELETE ON identity.registration_sources FROM identity_maintenance;
            REVOKE DELETE ON identity.send_counters FROM identity_maintenance;
            REVOKE DELETE ON identity.send_key_counters FROM identity_maintenance;
            REVOKE DELETE ON identity.sends FROM identity_maintenance;
            REVOKE DELETE ON identity.signin_challenges FROM identity_maintenance;
            REVOKE DELETE ON identity.throttle_counters FROM identity_maintenance;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            """
            GRANT DELETE ON identity.throttle_counters TO identity_maintenance;
            GRANT DELETE ON identity.signin_challenges TO identity_maintenance;
            GRANT DELETE ON identity.sends TO identity_maintenance;
            GRANT DELETE ON identity.send_key_counters TO identity_maintenance;
            GRANT DELETE ON identity.send_counters TO identity_maintenance;
            GRANT DELETE ON identity.registration_sources TO identity_maintenance;
            GRANT DELETE ON identity.nonexistence_notices TO identity_maintenance;
            GRANT DELETE ON identity.callbacks TO identity_maintenance;
            """);
    }
}
