using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// What erasure leaves behind: the rows, and nothing readable in them
/// (PRIV-RIGHT-005a, PRIV-RIGHT-005c, IDN-PRIN-003).
/// </summary>
/// <remarks>
/// The two writes are the subject's wrapped key and the fingerprints of its
/// identifiers, both rows <c>Janus.Storage</c> owns, and both are made in one
/// transaction.
/// </remarks>
[Trait("kind", "integration")]
public sealed class ErasureTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const byte Scheme = 0x01;
    private const byte Erased = 0x00;
    private const int DataKeyLength = 32;

    private const string CountIdentifiers =
        "SELECT count(*) FROM janus.identifiers WHERE subject = @subject";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] FingerprintKey =
        Encoding.UTF8.GetBytes("the fingerprint key of this deployment");

    /// <summary>
    /// PRIV-RIGHT-005c AC5 and PRIV-RIGHT-005a AC9: destroying the subject's key and
    /// neutralising its fingerprints are one transaction, after which the rows are
    /// still there, the encrypted columns no longer decrypt and no lookup matches.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005c_AC5_ErasureLeavesTheRowsAndNothingReadableAsync()
    {
        SubjectId subject = Subjects.New();
        KeyEncryptionKeys keys = OneVersion();

        await WriteAsync(subject, keys, "ahmed@example.com", "+201001234567");

        await using (StoreContext erasing = database.Context())
        await using (var work = new UnitOfWork(erasing))
        {
            await work.BeginAsync(TestContext.Current.CancellationToken);

            SubjectKeyRecord key = await erasing.SubjectKeys
                .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);
            key.FormatMarker = Erased;
            key.WrappedKey = new byte[DataKeyLength];

            List<IdentifierRecord> identifiers = await erasing.Identifiers
                .Where(row => row.Subject == subject)
                .ToListAsync(TestContext.Current.CancellationToken);

            foreach (IdentifierRecord identifier in identifiers)
            {
                identifier.Fingerprint = Fingerprint.Neutralised();
            }

            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            2,
            await connection.ExecuteScalarAsync<int>(CountIdentifiers, new { subject = subject.Value }));

        await using StoreContext reading = database.Context();
        SubjectKeyRecord erased = await reading.SubjectKeys
            .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);

        Assert.Equal(Erased, erased.FormatMarker);
        Assert.Throws<CryptographicException>(() =>
            PersonalFieldCipher.Unwrap(erased.FormatMarker, 1, erased.WrappedKey, keys));

        List<IdentifierRecord> rows = await reading.Identifiers
            .Where(row => row.Subject == subject)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.All(rows, row => Assert.True(Fingerprint.IsNeutralised(row.Fingerprint)));
        Assert.All(rows, row => Assert.NotEmpty(row.Entered));
        Assert.False(Fingerprint.Matches(
            rows[0].Fingerprint,
            Fingerprint.Compute(Encoding.UTF8.GetBytes("ahmed@example.com"), FingerprintKey)));
    }

    /// <summary>
    /// PRIV-RIGHT-005c AC5 and REG-SESS-005: one live fingerprint of a kind exists at a
    /// time, and a neutralised one is outside that rule, so two erased accounts having
    /// held the same address is not a collision.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005c_AC5_NeutralisedFingerprintsDoNotCollideAsync()
    {
        SubjectId first = Subjects.New();
        SubjectId second = Subjects.New();
        KeyEncryptionKeys keys = OneVersion();

        await WriteAsync(first, keys, "erased@example.com", "+201000000001");
        await NeutraliseAsync(first);

        await WriteAsync(second, keys, "erased@example.com", "+201000000002");
        await NeutraliseAsync(second);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            2,
            await connection.ExecuteScalarAsync<int>(CountIdentifiers, new { subject = first.Value }));
        Assert.Equal(
            2,
            await connection.ExecuteScalarAsync<int>(CountIdentifiers, new { subject = second.Value }));
    }

    /// <summary>
    /// REG-SESS-005: an identifier belongs to at most one account, so a second account
    /// does not take a live fingerprint another account holds.
    /// </summary>
    [Fact]
    public async Task REG_SESS_005_ALiveFingerprintBelongsToOneAccountAsync()
    {
        KeyEncryptionKeys keys = OneVersion();

        await WriteAsync(Subjects.New(), keys, "one.owner@example.com", "+201000000003");

        DbUpdateException refusal = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await WriteAsync(Subjects.New(), keys, "one.owner@example.com", "+201000000004"));

        Assert.Equal(
            "ux_identifiers_fingerprint",
            Assert.IsType<PostgresException>(refusal.InnerException).ConstraintName);
    }

    /// <summary>
    /// PRIV-SENS-002 AC2: every column of the schema that could hold text is read as
    /// text, and neither identifier appears in any of them, so a dump taken without
    /// the key-encryption key yields no personal field for any subject.
    /// </summary>
    [Fact]
    public async Task PRIV_SENS_002_AC2_ADumpOfTheSchemaHoldsNoPersonalFieldAsync()
    {
        const string email = "dumped@example.com";
        const string phone = "+201009998877";

        await WriteAsync(Subjects.New(), OneVersion(), email, phone);

        await using NpgsqlConnection connection = await database.OpenAsync();

        List<(string Table, string Column, string Type)> columns =
            [.. await connection.QueryAsync<(string, string, string)>(
                "SELECT c.table_name, c.column_name, c.data_type "
                    + "FROM information_schema.columns c "
                    + "JOIN information_schema.tables t "
                    + "ON t.table_schema = c.table_schema AND t.table_name = c.table_name "
                    + "WHERE c.table_schema = 'janus' AND t.table_type = 'BASE TABLE' "
                    + "AND c.data_type IN ('text', 'character varying', 'bytea', 'jsonb')")];

        var holding = new List<string>();

        foreach ((string table, string column, string type) in columns)
        {
            string read = type is "bytea"
                ? "encode(\"" + column + "\", 'escape')"
                : "\"" + column + "\"::text";

            long found = await connection.ExecuteScalarAsync<long>(
                "SELECT count(*) FROM janus.\"" + table + "\" WHERE " + read + " LIKE ANY(@sought)",
                new { sought = new[] { "%" + email + "%", "%" + phone + "%" } });

            if (found > 0)
            {
                holding.Add(table + "." + column);
            }
        }

        Assert.NotEmpty(columns);
        Assert.Empty(holding);
    }

    /// <summary>
    /// PRIV-SENS-002 AC3: the fields declared for filtering are not encrypted, so the
    /// database still answers a lookup over them and the work stays where the index
    /// is rather than moving into the application.
    /// </summary>
    [Fact]
    public async Task PRIV_SENS_002_AC3_AFieldDeclaredForFilteringIsStillQueriedInSqlAsync()
    {
        const string email = "filtered@example.com";

        SubjectId subject = Subjects.New();
        await WriteAsync(subject, OneVersion(), email, "+201009998866");

        await using NpgsqlConnection connection = await database.OpenAsync();

        Guid found = await connection.ExecuteScalarAsync<Guid>(
            "SELECT subject FROM janus.identifiers "
                + "WHERE fingerprint = @fingerprint AND kind = 'email' AND is_primary",
            new
            {
                fingerprint = Fingerprint.Compute(
                    Encoding.UTF8.GetBytes(CanonicalForm.Of(email)),
                    FingerprintKey),
            });

        Assert.Equal(subject.Value, found);
    }

    private static KeyEncryptionKeys OneVersion()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] material = new byte[DataKeyLength];
        randomness.GetBytes(material);

        return new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = material });
    }

    private static IdentifierRecord Written(
        SubjectId subject,
        IdentifierKind kind,
        string canonical,
        byte[] dataKey,
        RandomNumberGenerator randomness)
    {
        byte[] plaintext = Encoding.UTF8.GetBytes(canonical);

        return new IdentifierRecord
        {
            Id = IdentifierId.New(TimeProvider.System),
            Subject = subject,
            Kind = kind,
            Fingerprint = Fingerprint.Compute(plaintext, FingerprintKey),
            CanonicalisationVersion = CanonicalForm.UnicodeVersion,
            Entered = PersonalFieldCipher.Encrypt(
                dataKey,
                new PersonalFieldLocation(
                    subject,
                    IdentifierConfiguration.Table,
                    IdentifierConfiguration.EnteredColumn),
                plaintext,
                randomness),
            Canonical = PersonalFieldCipher.Encrypt(
                dataKey,
                new PersonalFieldLocation(
                    subject,
                    IdentifierConfiguration.Table,
                    IdentifierConfiguration.CanonicalColumn),
                plaintext,
                randomness),
            AddedAt = Noon,
            VerifiedAt = Noon,
            IsPrimary = true,
            IsLocked = false,
        };
    }

    private async Task WriteAsync(
        SubjectId subject,
        KeyEncryptionKeys keys,
        string email,
        string phone)
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);

        try
        {
            await using StoreContext context = database.Context();
            await using var work = new UnitOfWork(context);

            await work.BeginAsync(TestContext.Current.CancellationToken);

            context.Accounts.Add(new AccountRecord
            {
                Subject = subject,
                CreatedAt = Noon,
                State = AccountState.Active,
            });

            context.SubjectKeys.Add(new SubjectKeyRecord
            {
                Subject = subject,
                FormatMarker = Scheme,
                KeyVersion = keys.CurrentVersion,
                WrappedKey = PersonalFieldCipher.Wrap(dataKey, keys.Current.Span),
            });

            context.Identifiers.Add(Written(subject, IdentifierKind.Email, email, dataKey, randomness));
            context.Identifiers.Add(Written(subject, IdentifierKind.Phone, phone, dataKey, randomness));

            await work.CommitAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private async Task NeutraliseAsync(SubjectId subject)
    {
        await using StoreContext context = database.Context();

        List<IdentifierRecord> rows = await context.Identifiers
            .Where(row => row.Subject == subject)
            .ToListAsync(TestContext.Current.CancellationToken);

        foreach (IdentifierRecord row in rows)
        {
            row.Fingerprint = Fingerprint.Neutralised();
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
