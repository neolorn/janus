using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Mailboxes;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Mailboxes;
using Janus.Storage.Identity.Identifiers;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// The fingerprint key's rotation over every column that holds a keyed fingerprint.
/// </summary>
/// <param name="connections">Where the statements take their connection from.</param>
/// <param name="deployment">The deployment's data key, which a reserved mailbox's own key is wrapped under.</param>
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <remarks>
/// Implements OPS-SEC-003, PRIV-RIGHT-005c and OPS-MIG-003a (D-166, 318). A fingerprint
/// is computed again from the value held beside it, decrypted under the key it is held
/// under, and written back only where it and its version still stand as they were read,
/// so an erasure or a change made meanwhile is never overwritten and nothing is computed
/// twice. A stored fingerprint that is not the one its value computes under its version
/// is a defect, and stops the run rather than being replaced.
/// </remarks>
internal sealed class FingerprintRotationStore(
    DataConnections connections,
    DeploymentDataKeyStore deployment,
    IKeyRing ring) : IFingerprintRotationStore
{
    private const string First =
        """
        SELECT subject, format_marker, key_version, wrapped_key
        FROM identity.subject_keys
        ORDER BY subject
        LIMIT @count;
        """;

    private const string After =
        """
        SELECT subject, format_marker, key_version, wrapped_key
        FROM identity.subject_keys
        WHERE subject > @after
        ORDER BY subject
        LIMIT @count;
        """;

    private const string Keys =
        """
        SELECT subject, format_marker, key_version, wrapped_key
        FROM identity.subject_keys
        WHERE subject = ANY(@subjects);
        """;

    private const string Mailboxes =
        """
        SELECT id, holder, fingerprint_version, fingerprint, enc_canonical, wrapped_key
        FROM identity.mailboxes
        WHERE fingerprint_version <> @current AND fingerprint <> @neutral
        ORDER BY id
        LIMIT @count;
        """;

    private const string Holds =
        """
        SELECT count(*)::int FROM identity.username_holds
        WHERE fingerprint_version <> @current AND releases_at > @now;
        """;

    // The columns whose fingerprint is computed from a value under the subject's own key,
    // each by its table, the column that keys its row, the fingerprint, the value, and
    // what makes a row one that is still read.
    private static readonly SubjectColumn[] Subjects =
    [
        new(IdentifierConfiguration.Table, "identifier_id", "fingerprint", IdentifierConfiguration.CanonicalColumn, "TRUE"),
        new(IdentifierRemovalConfiguration.Table, "identifier_id", "fingerprint", IdentifierRemovalConfiguration.CanonicalColumn, "expires_at > @now"),
        new(AuthenticatorConfiguration.Table, "id", "provider_subject", AuthenticatorConfiguration.ProviderSubjectColumn, "TRUE"),
    ];

    // The ledgers, whose keys are hashed from values the library never holds, and the
    // sign-ins in progress, which carry the hash of the identifier they were opened with
    // and are forgotten with the version it was computed under. A sign-in the previous
    // release opened carries no hash and no version, and is left to lapse.
    private static readonly string[] Ledgers =
    [
        "callbacks",
        "nonexistence_notices",
        "registration_sources",
        "send_counters",
        "send_grants",
        "send_key_counters",
        "sends",
        "signin_challenges",
        "throttle_counters",
    ];

    // D-166, 318: the abuse counts, each of which still counts until the expiry sweep
    // finds that its own check no longer reads it, so a retirement waits on every line
    // under a previous version rather than forgetting one that counts.
    private static readonly string[] Counts =
    [
        "callbacks",
        "nonexistence_notices",
        "registration_sources",
        "send_counters",
        "send_key_counters",
        "sends",
        "throttle_counters",
    ];

    private static readonly byte[] Neutral = Fingerprint.Neutralised();

    /// <inheritdoc/>
    public async ValueTask<IReadOnlySet<int>> FingerprintVersionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        string computing = string.Join(
            " UNION ",
            [
                .. Subjects.Select(column => column.Versions),
                "SELECT fingerprint_version FROM identity.mailboxes WHERE fingerprint <> @neutral",
                "SELECT fingerprint_version FROM identity.username_holds WHERE releases_at > @now",
                .. Ledgers.Select(ledger =>
                    $"SELECT fingerprint_version FROM identity.{ledger} WHERE fingerprint_version IS NOT NULL"),
            ]);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<int> versions = await ambient.Connection
            .QueryAsync<int>(new CommandDefinition(
                computing + ";",
                new { neutral = Neutral, now },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return versions.ToHashSet();
    }

    /// <inheritdoc/>
    public async ValueTask<KeyRotationBatch> RecomputeSubjectsAfterAsync(
        SubjectKeyId? after,
        int count,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectKey> keys = after is SubjectKeyId last
            ? await SubjectKeysAsync(After, new { after = last.Value, count }, cancellationToken).ConfigureAwait(false)
            : await SubjectKeysAsync(First, new { count }, cancellationToken).ConfigureAwait(false);

        if (keys.Count == 0)
        {
            return new KeyRotationBatch(Last: null, Processed: 0);
        }

        Guid[] live = [.. keys.Where(key => !key.IsErased).Select(key => key.Id.Value)];
        int recomputed = 0;

        foreach (SubjectColumn column in Subjects)
        {
            IReadOnlyList<Stale> stale = await StaleAsync(
                    column.OfSubjects,
                    new { subjects = live, current = Fingerprint.CurrentVersion(ring), neutral = Neutral, now },
                    cancellationToken)
                .ConfigureAwait(false);

            recomputed += await RecomputedAsync(column, stale, keys, cancellationToken).ConfigureAwait(false);
        }

        return new KeyRotationBatch(keys[^1].Id, recomputed);
    }

    /// <inheritdoc/>
    public async ValueTask<int> RecomputeRemainingAsync(
        int count,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        int recomputed = 0;

        foreach (SubjectColumn column in Subjects)
        {
            if (recomputed == count)
            {
                return recomputed;
            }

            IReadOnlyList<Stale> stale = await StaleAsync(
                    column.Remaining,
                    new
                    {
                        current = Fingerprint.CurrentVersion(ring),
                        neutral = Neutral,
                        marker = (short)PersonalDataFormat.Marker,
                        now,
                        count = count - recomputed,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<SubjectKey> keys = await SubjectKeysAsync(
                    Keys,
                    new { subjects = stale.Select(row => row.Subject).Distinct().ToArray() },
                    cancellationToken)
                .ConfigureAwait(false);

            recomputed += await RecomputedAsync(column, stale, keys, cancellationToken).ConfigureAwait(false);
        }

        return recomputed + await RecomputedMailboxesAsync(count - recomputed, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<int> StandingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        string standing = "SELECT ("
            + string.Join(
                " + ",
                [
                    .. Subjects.Select(column => $"({column.Standing})"),
                    "(SELECT count(*) FROM identity.mailboxes WHERE fingerprint_version <> @current AND fingerprint <> @neutral)",
                    .. Counts.Select(ledger =>
                        $"(SELECT count(*) FROM identity.{ledger} WHERE fingerprint_version <> @current)"),
                ])
            + ")::int;";

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new { current = Fingerprint.CurrentVersion(ring), neutral = Neutral, now };

        int stale = await ambient.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(standing, parameters, ambient.Transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return stale + await ambient.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(Holds, parameters, ambient.Transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ForgetAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new { current = Fingerprint.CurrentVersion(ring), now };

        foreach (string ledger in Ledgers)
        {
            await ambient.Connection
                .ExecuteAsync(new CommandDefinition(
                    $"DELETE FROM identity.{ledger} WHERE fingerprint_version <> @current;",
                    parameters,
                    ambient.Transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                "DELETE FROM identity.username_holds WHERE fingerprint_version <> @current AND releases_at <= @now;",
                parameters,
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyList<SubjectKey>> SubjectKeysAsync(
        string query,
        object parameters,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<(Guid Subject, short Marker, int Version, byte[] Wrapped)> rows = await ambient.Connection
            .QueryAsync<(Guid, short, int, byte[])>(new CommandDefinition(
                query,
                parameters,
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => SubjectKey.Existing(new SubjectKeyId(row.Subject), (byte)row.Marker, row.Version, row.Wrapped)),
        ];
    }

    private async ValueTask<IReadOnlyList<Stale>> StaleAsync(
        string query,
        object parameters,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<(Guid Key, Guid Subject, int Version, byte[] Fingerprint, byte[] Value)> rows = await ambient.Connection
            .QueryAsync<(Guid, Guid, int, byte[], byte[])>(new CommandDefinition(
                query,
                parameters,
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return [.. rows.Select(row => new Stale(row.Key, row.Subject, row.Version, row.Fingerprint, row.Value))];
    }

    // The fingerprints of one column computed again, each under the key of the subject
    // whose value it is, unwrapped once for the batch and cleared before it returns.
    private async ValueTask<int> RecomputedAsync(
        SubjectColumn column,
        IReadOnlyList<Stale> stale,
        IReadOnlyList<SubjectKey> keys,
        CancellationToken cancellationToken)
    {
        int recomputed = 0;

        foreach (IGrouping<Guid, Stale> ofSubject in stale.GroupBy(row => row.Subject))
        {
            var subject = new SubjectId(ofSubject.Key);
            SubjectKey key = keys.Single(held => held.Id == SubjectKeyId.Of(subject));
            byte[] dataKey = PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, ring);

            try
            {
                foreach (Stale row in ofSubject)
                {
                    var location = new PersonalFieldLocation(subject, column.Table, column.Value);

                    recomputed += await RecomputedAsync(column.Recompute, row, dataKey, location, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }
        }

        return recomputed;
    }

    // The mailboxes' addresses: under the holder's key while the mailbox is held, under
    // the row's own otherwise, wrapped under the deployment's data key, which is unwrapped
    // once for the batch. One whose holder was erased has a neutralised fingerprint and
    // is not read.
    private async ValueTask<int> RecomputedMailboxesAsync(int count, CancellationToken cancellationToken)
    {
        if (count == 0)
        {
            return 0;
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        List<(Guid Id, Guid? Holder, int Version, byte[] Fingerprint, byte[] Value, byte[]? WrappedKey)> rows =
        [
            .. await ambient.Connection
                .QueryAsync<(Guid, Guid?, int, byte[], byte[], byte[]?)>(new CommandDefinition(
                    Mailboxes,
                    new { current = Fingerprint.CurrentVersion(ring), neutral = Neutral, count },
                    ambient.Transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false),
        ];

        byte[] deploymentKey = rows.Exists(mailbox => mailbox.Holder is null)
            ? await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false)
            : [];

        try
        {
            return await RecomputedMailboxesAsync(rows, deploymentKey, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deploymentKey);
        }
    }

    private async ValueTask<int> RecomputedMailboxesAsync(
        List<(Guid Id, Guid? Holder, int Version, byte[] Fingerprint, byte[] Value, byte[]? WrappedKey)> rows,
        byte[] deploymentKey,
        CancellationToken cancellationToken)
    {
        int recomputed = 0;

        foreach ((Guid Id, Guid? Holder, int Version, byte[] Fingerprint, byte[] Value, byte[]? WrappedKey) mailbox
            in rows)
        {
            byte[] dataKey = mailbox.Holder is Guid held
                ? await HolderKeyAsync(new SubjectId(held), cancellationToken).ConfigureAwait(false)
                : PersonalFieldCipher.Unwrap(
                    mailbox.WrappedKey ?? throw new InvalidOperationException("The mailbox has no key."),
                    deploymentKey);

            try
            {
                PersonalFieldLocation location = MailboxStore.Located(
                    new MailboxId(mailbox.Id),
                    mailbox.Holder is Guid of ? new SubjectId(of) : null);

                recomputed += await RecomputedAsync(
                        MailboxColumn.Recompute,
                        new Stale(mailbox.Id, mailbox.Holder ?? Guid.Empty, mailbox.Version, mailbox.Fingerprint, mailbox.Value),
                        dataKey,
                        location,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }
        }

        return recomputed;
    }

    private async ValueTask<byte[]> HolderKeyAsync(SubjectId holder, CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectKey> keys = await SubjectKeysAsync(Keys, new { subjects = new[] { holder.Value } }, cancellationToken)
            .ConfigureAwait(false);

        SubjectKey key = keys.SingleOrDefault()
            ?? throw new InvalidOperationException("The holder has no key to read the mailbox under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, ring);
    }

    // One fingerprint computed again from its value, written back only where it and its
    // version still stand as they were read.
    private async ValueTask<int> RecomputedAsync(
        string recompute,
        Stale row,
        byte[] dataKey,
        PersonalFieldLocation location,
        CancellationToken cancellationToken)
    {
        byte[] value = PersonalFieldCipher.Decrypt(dataKey, location, row.Value);
        byte[] fingerprint;

        try
        {
            bool computes = ring
                .BorrowFingerprintKeys(keys => keys.Versions.TryGetValue(row.Version, out ReadOnlyMemory<byte> previous)
                    ? CryptographicOperations.FixedTimeEquals(Fingerprint.Compute(value, previous.Span), row.Fingerprint)
                    : throw new InvalidOperationException("A fingerprint is under a version the command was not handed."))
                .Match(matched => matched, error => throw new InvalidOperationException(error.Code.ToString()));

            if (!computes)
            {
                throw new InvalidOperationException("A stored fingerprint is not the one its value computes.");
            }

            fingerprint = Fingerprint.Compute(value, ring);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(value);
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        return await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                recompute,
                new
                {
                    key = row.Key,
                    previous = row.Version,
                    stale = row.Fingerprint,
                    current = Fingerprint.CurrentVersion(ring),
                    fingerprint,
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    // A fingerprint read under a previous version, with what it was computed from.
    private sealed record Stale(Guid Key, Guid Subject, int Version, byte[] Fingerprint, byte[] Value);

    // The mailboxes' column, whose value is under the holder's key or the row's own.
    private static class MailboxColumn
    {
        public const string Recompute =
            "UPDATE identity.mailboxes SET fingerprint = @fingerprint, fingerprint_version = @current "
            + "WHERE id = @key AND fingerprint_version = @previous AND fingerprint = @stale;";
    }

    // A column holding a fingerprint computed from a value under the subject's key, with
    // the statements the rotation runs over it. The names are this class's own constants.
    private sealed record SubjectColumn(string Table, string Key, string Fingerprint, string Value, string Live)
    {
        public string Versions { get; } =
            $"SELECT fingerprint_version FROM identity.{Table} "
            + $"WHERE fingerprint_version IS NOT NULL AND {Fingerprint} <> @neutral AND {Live}";

        public string OfSubjects { get; } =
            $"SELECT {Key}, subject, fingerprint_version, {Fingerprint}, {Value} FROM identity.{Table} "
            + $"WHERE subject = ANY(@subjects) AND fingerprint_version <> @current AND {Fingerprint} <> @neutral AND {Live};";

        public string Remaining { get; } =
            $"SELECT stored.{Key}, stored.subject, stored.fingerprint_version, stored.{Fingerprint}, stored.{Value} "
            + $"FROM identity.{Table} stored JOIN identity.subject_keys held "
            + "ON held.subject = stored.subject AND held.format_marker = @marker "
            + $"WHERE stored.fingerprint_version <> @current AND stored.{Fingerprint} <> @neutral AND {Live} "
            + $"ORDER BY stored.{Key} LIMIT @count;";

        public string Standing { get; } =
            $"SELECT count(*) FROM identity.{Table} "
            + $"WHERE fingerprint_version <> @current AND {Fingerprint} <> @neutral AND {Live}";

        public string Recompute { get; } =
            $"UPDATE identity.{Table} SET {Fingerprint} = @fingerprint, fingerprint_version = @current "
            + $"WHERE {Key} = @key AND fingerprint_version = @previous AND {Fingerprint} = @stale;";
    }
}
