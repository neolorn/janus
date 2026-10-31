using System;
using System.Threading.Tasks;
using Dapper;
using Janus.Storage.Authentication.Maintenance;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the key-encryption key's cryptoperiod is read from (DR-009a).
/// </summary>
[Trait("kind", "integration")]
public sealed class CryptoperiodStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// DR-009a AC1, AC6: the key-encryption key's cryptoperiod is read from the trail:
    /// the latest completed rotation of that key, which a later rotation of the
    /// fingerprint key does not displace, and bootstrap's record of the first
    /// organization, which a person's creation of another does not displace.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_TheCryptoperiodIsReadFromTheKeysOwnRotationRecordAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Null(await RotatedAsync());

        await RecordedAsync(connection, "identity.organization.created", Noon.AddDays(-20), "bootstrap", "{}");
        await RecordedAsync(connection, "identity.organization.created", Noon.AddDays(-21), "account-deletion", "{}");
        await RecordedAsync(connection, "ops.keyrotation.completed", Noon.AddDays(-10), "rotate-kek", """{"kind":"key-encryption-key","version":2,"processed":4}""");
        await RecordedAsync(connection, "ops.keyrotation.completed", Noon.AddDays(-5), "rotate-fingerprint-key", """{"kind":"fingerprint-key","version":2,"processed":0}""");

        Assert.Equal((2, Noon.AddDays(-10)), await RotatedAsync());
        Assert.Equal(Noon.AddDays(-20), await BootstrappedAsync());

        await RecordedAsync(connection, "ops.keyrotation.completed", Noon.AddDays(10), "rotate-kek", """{"kind":"key-encryption-key","version":3,"processed":4}""");

        Assert.Equal((3, Noon.AddDays(10)), await RotatedAsync());
    }

    private static async Task RecordedAsync(
        NpgsqlConnection connection,
        string action,
        DateTimeOffset at,
        string principal,
        string details) =>
        await connection.ExecuteAsync(
            """
            INSERT INTO identity.audit_records
                (id, category, occurred_at, action, acting_subject, effective_subject, details, principal, principal_reason)
            VALUES (gen_random_uuid(), 'security', @at, @action, @nobody, @nobody, CAST(@details AS jsonb), @principal, 'OPS-SEC-003');
            """,
            new { at, action, nobody = Guid.Empty, details, principal });

    private async Task<(int Version, DateTimeOffset CompletedAt)?> RotatedAsync()
    {
        await using StoreContext reading = database.Context();

        return await new CryptoperiodStore(reading).KeyEncryptionKeyRotatedAsync(TestContext.Current.CancellationToken);
    }

    private async Task<DateTimeOffset?> BootstrappedAsync()
    {
        await using StoreContext reading = database.Context();

        return await new CryptoperiodStore(reading).BootstrappedAsync(TestContext.Current.CancellationToken);
    }
}
