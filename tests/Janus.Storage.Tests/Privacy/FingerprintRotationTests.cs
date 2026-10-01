using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Sending;
using Janus.Authentication.SignIn;
using Janus.Core;
using Janus.Identity.Identifiers;
using Janus.Privacy;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Callbacks;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Mailboxes;
using Janus.Storage.Authentication.Sending;
using Janus.Storage.Authentication.SignIn;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// What the fingerprint key's rotation does to the fingerprints a deployment holds: each
/// is computed again under the new version from the value beside it, in batches that
/// survive a killed run, a lookup matches under every version held meanwhile, and the
/// previous version retires once nothing still read stands under it (OPS-SEC-003 AC6).
/// </summary>
/// <remarks>
/// The rotation runs over every fingerprint in the database, so each case begins with
/// none and no rotation recorded. The rotation runs under the maintenance credential,
/// as the command runs it.
/// </remarks>
[Trait("kind", "integration")]
public sealed class FingerprintRotationTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    // OPS-SEC-003, D-153: the subjects one transaction takes.
    private const int Batch = 500;

    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Next = RandomNumberGenerator.GetBytes(32);

    // Chapter 10 section 4: the default half-life of a throttle counter.
    private static readonly TimeSpan Decay = TimeSpan.FromMinutes(10);

    // What the application and the command hold once the new version is current.
    private static readonly FingerprintKeys Rotating = new(
        2,
        new Dictionary<int, ReadOnlyMemory<byte>> { [1] = Deployment.FingerprintKey, [2] = Next });

    // What is left once the previous version is retired.
    private static readonly FingerprintKeys Retired = new(
        2,
        new Dictionary<int, ReadOnlyMemory<byte>> { [2] = Next });

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync(
            """
            TRUNCATE identity.key_rotations, identity.accounts, identity.subject_keys, identity.identifiers,
                identity.identifier_removals, identity.authenticators, identity.mailboxes, identity.username_holds,
                identity.throttle_counters, identity.callbacks, identity.nonexistence_notices,
                identity.registration_sources, identity.send_counters, identity.send_key_counters,
                identity.sends CASCADE;
            DELETE FROM identity.audit_records WHERE action LIKE 'ops.keyrotation.%';
            """);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _deployment.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// OPS-SEC-003 AC6: an application handed the new version as current, the previous
    /// one kept beside it, finds what was written under the previous one: the owner of
    /// an identifier, a reserved address, a provider's link, a mailbox, a held username
    /// and a throttle counter.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_ALookupMatchesUnderEveryVersionHeldAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Seeded seeded = await SeedAsync();
        const string username = "held.username";

        await HeldAsync(username, 1, Noon.AddDays(30));

        await using (StoreContext counting = database.Context())
        {
            await new ThrottleLedger(counting, new DataConnections(counting), Deployment.Fingerprints)
                .FailedAsync(ThrottleScope.Source, "192.0.2.1", standing: 2, Noon, cancellationToken);
            await counting.SaveChangesAsync(cancellationToken);
        }

        await using StoreContext reading = database.Context();
        IdentifierStore identifiers = Identifiers(reading, Rotating);

        Assert.Equal(seeded.Subject, await identifiers.FindOwnerAsync(IdentifierKind.Email, seeded.Kept, cancellationToken));
        Assert.True(await identifiers.IsReservedAsync(IdentifierKind.Email, seeded.Given, Noon, cancellationToken));
        Assert.True(await identifiers.IsHeldAsync(username, Noon, cancellationToken));
        Assert.Equal(
            seeded.Linked,
            (await Authenticators(reading, Rotating).ByProviderAsync(Factor.Google, seeded.ProviderSubject, cancellationToken))?.Subject);
        Assert.NotNull(await Mailboxes(reading, Rotating).FindAsync(seeded.Reserved, cancellationToken));
        Assert.NotNull(await Mailboxes(reading, Rotating).FindAsync(seeded.Held, cancellationToken));
        Assert.Equal(
            new ThrottleCounter(3, Noon),
            await new ThrottleLedger(reading, new DataConnections(reading), Ring(Rotating)).FindAsync(ThrottleScope.Source, "192.0.2.1", cancellationToken));
    }

    /// <summary>
    /// OPS-SEC-003 AC6: once the rotation completes, every fingerprint computed from a
    /// value the library holds is under the new version and is that value's under the
    /// new key, so a deployment holding the new version alone finds each of them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_EveryFingerprintIsComputedAgainUnderTheNewVersionAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Seeded seeded = await SeedAsync();

        KeyRotationProgress progress = Completed(await RecomputedAsync(new FixedTime(Noon), cancellationToken));

        Assert.Equal(5, progress.Processed);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            [2],
            await connection.QueryAsync<int>(
                """
                SELECT fingerprint_version FROM identity.identifiers
                UNION SELECT fingerprint_version FROM identity.identifier_removals
                UNION SELECT fingerprint_version FROM identity.authenticators
                UNION SELECT fingerprint_version FROM identity.mailboxes
                """));
        Assert.Equal(
            Hashed(seeded.Kept),
            await connection.QuerySingleAsync<byte[]>("SELECT fingerprint FROM identity.identifiers"));
        Assert.Equal(
            Hashed(seeded.Given),
            await connection.QuerySingleAsync<byte[]>("SELECT fingerprint FROM identity.identifier_removals"));
        Assert.Equal(
            Hashed(seeded.ProviderSubject),
            await connection.QuerySingleAsync<byte[]>("SELECT provider_subject FROM identity.authenticators"));
        Assert.Equal(
            new[] { Hashed(seeded.Held.Value), Hashed(seeded.Reserved.Value) }
                .Select(Convert.ToHexString)
                .Order(StringComparer.Ordinal),
            (await connection.QueryAsync<byte[]>("SELECT fingerprint FROM identity.mailboxes"))
                .Select(Convert.ToHexString)
                .Order(StringComparer.Ordinal));

        await using StoreContext reading = database.Context();
        IdentifierStore identifiers = Identifiers(reading, Retired);

        Assert.Equal(seeded.Subject, await identifiers.FindOwnerAsync(IdentifierKind.Email, seeded.Kept, cancellationToken));
        Assert.True(await identifiers.IsReservedAsync(IdentifierKind.Email, seeded.Given, Noon, cancellationToken));
        Assert.Equal(
            seeded.Linked,
            (await Authenticators(reading, Retired).ByProviderAsync(Factor.Google, seeded.ProviderSubject, cancellationToken))?.Subject);
        Assert.NotNull(await Mailboxes(reading, Retired).FindAsync(seeded.Reserved, cancellationToken));
        Assert.NotNull(await Mailboxes(reading, Retired).FindAsync(seeded.Held, cancellationToken));
    }

    /// <summary>
    /// OPS-SEC-003 AC2, AC6: a run killed mid-batch leaves the batches it committed and
    /// the point it reached; the fingerprints it had not reached are still found under
    /// the previous version; run again, it resumes from that point, and when it reports
    /// complete every fingerprint is under the new version, computed once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_AKilledRunResumesFromItsProgressAndComputesEachFingerprintOnceAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const int count = Batch + 20;
        var canonical = new Dictionary<Guid, string>(count);

        for (int at = 0; at < count; at++)
        {
            SubjectId subject = await _deployment.AccountAsync(Noon);
            canonical[subject.Value] = Canonicalised(await WrittenAsync(subject, Fresh("Rotated")));
        }

        await using NpgsqlConnection connection = await database.OpenAsync();
        Guid[] ordered = [.. await connection.QueryAsync<Guid>("SELECT subject FROM identity.subject_keys ORDER BY subject")];

        // An identifier of the second batch is held by another transaction, so the run
        // stops on it with the first batch committed and the second in hand.
        await using NpgsqlConnection holder = await database.OpenAsync();
        await using NpgsqlTransaction holding = await holder.BeginTransactionAsync(cancellationToken);
        await holder.ExecuteAsync(
            "SELECT 1 FROM identity.identifiers WHERE subject = @subject FOR UPDATE",
            new { subject = ordered[Batch + 10] },
            holding);

        using var killed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<Result<KeyRotationProgress>> run = RecomputedAsync(TimeProvider.System, killed.Token);

        await ProcessedAsync(connection, Batch);
        await killed.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        await holding.RollbackAsync(cancellationToken);

        (int processed, Guid last) = await connection.QuerySingleAsync<(int, Guid)>(
            "SELECT processed, last_subject FROM identity.key_rotations");

        Assert.Equal(Batch, processed);
        Assert.Equal(ordered[Batch - 1], last);
        Assert.Equal(ordered[..Batch], await SubjectsUnderAsync(connection, 2));

        await using (StoreContext reading = database.Context())
        {
            IdentifierStore identifiers = Identifiers(reading, Rotating);

            foreach (Guid subject in ordered[Batch..])
            {
                Assert.Equal(
                    new SubjectId(subject),
                    await identifiers.FindOwnerAsync(IdentifierKind.Email, canonical[subject], cancellationToken));
            }
        }

        KeyRotationProgress resumed = Completed(await RecomputedAsync(TimeProvider.System, cancellationToken));

        Assert.Equal(count, resumed.Processed);
        Assert.Equal(ordered, await SubjectsUnderAsync(connection, 2));

        foreach ((Guid subject, byte[] fingerprint) in await connection.QueryAsync<(Guid, byte[])>(
            "SELECT subject, fingerprint FROM identity.identifiers"))
        {
            Assert.Equal(Hashed(canonical[subject]), fingerprint);
        }

        Assert.Equal(
            [("ops.keyrotation.started", 0), ("ops.keyrotation.resumed", Batch), ("ops.keyrotation.completed", count)],
            await connection.QueryAsync<(string, int)>(
                """
                SELECT action, (details->>'processed')::int
                FROM identity.audit_records
                WHERE action LIKE 'ops.keyrotation.%' AND details->>'kind' = 'fingerprint-key'
                ORDER BY occurred_at, id
                """));
    }

    /// <summary>
    /// OPS-SEC-003 AC6: a held username is a fingerprint no value stands behind, so the
    /// previous version is not retired while one is held under it, nor while a throttle
    /// counter under it still stands; once the username is released and the sweep has
    /// removed the counter that decayed, the retirement forgets the hold and keeps what
    /// is counted under the new version.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_RetirementWaitsForAHeldUsernameAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTimeOffset releases = Noon.AddDays(1);

        await HeldAsync("held.username", 1, releases);

        await using (StoreContext counting = database.Context())
        {
            await new ThrottleLedger(counting, new DataConnections(counting), Deployment.Fingerprints)
                .FailedAsync(ThrottleScope.Source, "192.0.2.1", standing: 0, Noon, cancellationToken);
            await new ThrottleLedger(counting, new DataConnections(counting), Ring(Rotating))
                .FailedAsync(ThrottleScope.Source, "192.0.2.2", standing: 0, releases, cancellationToken);
            await counting.SaveChangesAsync(cancellationToken);
        }

        Completed(await RecomputedAsync(new FixedTime(Noon), cancellationToken));

        Error refused = Refusal(await RetiredAsync(new FixedTime(Noon), cancellationToken));

        Assert.Equal(ErrorCodes.RotationNotReady, refused.Code);
        Assert.Equal(2, refused.Details["pending"].GetInt32());

        await using (StoreContext sweeping = database.Context())
        {
            await new ThrottleLedger(sweeping, new DataConnections(sweeping), Ring(Rotating)).SweepAsync(releases, Decay, cancellationToken);
        }

        KeyRetirement retirement = Retirement(await RetiredAsync(new FixedTime(releases), cancellationToken));

        Assert.Equal([1], retirement.Retired);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM identity.username_holds"));
        Assert.Equal(
            [2],
            await connection.QueryAsync<int>("SELECT fingerprint_version FROM identity.throttle_counters"));
    }

    /// <summary>
    /// OPS-SEC-003 AC6 (D-166, 318): an abuse count under the previous version still
    /// counts, so the retirement waits on every one of them, a line of each ledger, and
    /// goes ahead once the expiry sweep has found that no check reads them any more.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_RetirementWaitsWhileAnAbuseCountUnderThePreviousVersionStillCountsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTimeOffset later = Noon.AddDays(2);

        await using (StoreContext counting = database.Context())
        {
            await new ThrottleLedger(counting, new DataConnections(counting), Deployment.Fingerprints)
                .FailedAsync(ThrottleScope.Source, "192.0.2.9", standing: 0, Noon, cancellationToken);
            await new SendLedger(counting, new DataConnections(counting), Deployment.Fingerprints).RecordAsync(
                RandomNumberGenerator.GetBytes(32),
                [
                    new SendCount(
                        new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, "+201001234579"),
                        TimeSpan.FromDays(1)),
                    new SendCount(
                        new RestrictionKey("sms.source", RestrictionKeyKind.Source, "198.51.100.9"),
                        TimeSpan.FromDays(1)),
                ],
                [],
                Noon,
                cancellationToken);
            await new RegistrationSourceLedger(counting, Deployment.Fingerprints)
                .RecordAsync("198.51.100.9", Noon, cancellationToken);
            _ = await new NoticeLedger(counting, Deployment.Fingerprints)
                .FirstAsync("nobody@example.test", Noon, TimeSpan.FromHours(1), cancellationToken);
            _ = await new CallbackLedger(counting, Deployment.Fingerprints)
                .ReceivedAsync("203.0.113.9", Noon, TimeSpan.FromMinutes(1), cancellationToken);
            await counting.SaveChangesAsync(cancellationToken);
        }

        Completed(await RecomputedAsync(new FixedTime(Noon), cancellationToken));

        Error refused = Refusal(await RetiredAsync(new FixedTime(Noon), cancellationToken));

        Assert.Equal(ErrorCodes.RotationNotReady, refused.Code);
        Assert.Equal(7, refused.Details["pending"].GetInt32());

        await using (StoreContext sweeping = database.Context())
        {
            var sends = new SendLedger(sweeping, new DataConnections(sweeping), Ring(Rotating));

            await new ThrottleLedger(sweeping, new DataConnections(sweeping), Ring(Rotating)).SweepAsync(later, Decay, cancellationToken);
            await sends.SweepSettledAsync(later, cancellationToken);
            await sends.SweepAsync(new CounterStaleness(later.AddDays(-1), later.AddDays(-1)), cancellationToken);
            await new RegistrationSourceLedger(sweeping, Ring(Rotating)).SweepAsync(later, cancellationToken);
            await new NoticeLedger(sweeping, Ring(Rotating)).SweepAsync(later, TimeSpan.FromHours(1), cancellationToken);
            await new CallbackLedger(sweeping, Ring(Rotating)).SweepAsync(later, cancellationToken);
        }

        Assert.Equal([1], Retirement(await RetiredAsync(new FixedTime(later), cancellationToken)).Retired);
    }

    /// <summary>
    /// OPS-SEC-003 AC6, AUTH-ABUSE-001 (D-166, 318): the sweep forgets a throttle counter
    /// once its standing has decayed to nothing and not a second before, the database
    /// deciding as <see cref="Throttle.Standing"/> does.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_TheSweepForgetsAThrottleCounterOnceItStandsAtNothingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTimeOffset now = Noon.AddDays(1);
        List<(string Key, ThrottleCounter Counter)> written = [];

        await using (StoreContext counting = database.Context())
        {
            var ledger = new ThrottleLedger(counting, new DataConnections(counting), Ring(Rotating));

            foreach (int failures in new[] { 1, 3, 8 })
            {
                TimeSpan nothing = Decay * Math.Log2(2.0 * failures);

                foreach (TimeSpan age in new[] { nothing - TimeSpan.FromSeconds(1), nothing + TimeSpan.FromSeconds(1) })
                {
                    var counter = new ThrottleCounter(failures, now - age);
                    string key = $"192.0.2.{written.Count + 10}";

                    await ledger.FailedAsync(ThrottleScope.Source, key, failures - 1, counter.At, cancellationToken);
                    written.Add((key, counter));
                }
            }

            await counting.SaveChangesAsync(cancellationToken);
        }

        await using (StoreContext sweeping = database.Context())
        {
            await new ThrottleLedger(sweeping, new DataConnections(sweeping), Ring(Rotating)).SweepAsync(now, Decay, cancellationToken);
        }

        await using StoreContext reading = database.Context();
        var read = new ThrottleLedger(reading, new DataConnections(reading), Ring(Rotating));
        List<bool> kept = [];

        foreach ((string key, ThrottleCounter _) in written)
        {
            kept.Add(await read.FindAsync(ThrottleScope.Source, key, cancellationToken) is not null);
        }

        Assert.Equal([true, false, true, false, true, false], kept);
        Assert.Equal(
            kept,
            written.Select(line => Throttle.Standing(line.Counter, now, Decay) > 0));
    }

    /// <summary>
    /// OPS-SEC-003 AC6, AUTH-ABUSE-001: a sign-in in progress carries the hash of the
    /// identifier it was opened with, so the retirement forgets one opened under the
    /// previous version as it forgets a ledger line and keeps one opened under the new
    /// one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_RetirementForgetsTheSignInsOpenedUnderThePreviousVersionAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Challenge previous = Opened();
        Challenge current = Opened();

        await using (StoreContext opening = database.Context())
        {
            await new ChallengeStore(opening, Deployment.Fingerprints).AddAsync(previous, cancellationToken);
            await new ChallengeStore(opening, Ring(Rotating)).AddAsync(current, cancellationToken);
            _ = await opening.SaveChangesAsync(cancellationToken);
        }

        Completed(await RecomputedAsync(new FixedTime(Noon), cancellationToken));

        Assert.Equal([1], Retirement(await RetiredAsync(new FixedTime(Noon), cancellationToken)).Retired);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            Convert.ToHexString(current.Fingerprint),
            Convert.ToHexString(
                Assert.Single(await connection.QueryAsync<byte[]>("SELECT handle FROM identity.signin_challenges"))));
    }

    /// <summary>
    /// OPS-SEC-003 AC6: an address an erased subject gave up stays reserved until its
    /// undo lapses, and no key is left to compute it again under, so the previous
    /// version is not retired until the reservation lapses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_RetirementWaitsForTheReservationOfAnErasedSubjectAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Seeded seeded = await SeedAsync();
        DateTimeOffset lapses = Noon.AddHours(72);

        // As the erasure transaction leaves the subject: the key destroyed and the
        // fingerprints of what it holds neutralised (PRIV-RIGHT-005c).
        await _deployment.EraseAsync(seeded.Subject);
        await using NpgsqlConnection connection = await database.OpenAsync();
        await connection.ExecuteAsync(
            """
            UPDATE identity.identifiers SET fingerprint = @neutral WHERE subject = @subject;
            UPDATE identity.mailboxes SET fingerprint = @neutral WHERE holder = @subject;
            """,
            new { neutral = Fingerprint.Neutralised(), subject = seeded.Subject.Value });

        Completed(await RecomputedAsync(new FixedTime(Noon), cancellationToken));

        Error refused = Refusal(await RetiredAsync(new FixedTime(Noon), cancellationToken));

        Assert.Equal(ErrorCodes.RotationNotReady, refused.Code);
        Assert.Equal(1, refused.Details["pending"].GetInt32());
        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<int>("SELECT fingerprint_version FROM identity.identifier_removals"));

        Assert.Equal([1], Retirement(await RetiredAsync(new FixedTime(lapses), cancellationToken)).Retired);
    }

    /// <summary>
    /// OPS-SEC-003 AC2, AC6, CONV-DESIGN-003 AC6: two runs at once each read the rotation
    /// with its progress held, so it is started once and resumed by the other, each
    /// fingerprint is counted once, and the completion is recorded once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_TwoRunsAtOnceStartAndCompleteTheRotationOnceAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync();

        Result<KeyRotationProgress>[] runs = await Task.WhenAll(
            RecomputedAsync(new FixedTime(Noon), cancellationToken),
            RecomputedAsync(new FixedTime(Noon), cancellationToken));

        Assert.All(runs, run => Assert.Equal(5, Completed(run).Processed));

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            [("ops.keyrotation.completed", 5), ("ops.keyrotation.resumed", 1), ("ops.keyrotation.started", 1)],
            (await connection.QueryAsync<(string Action, int Processed)>(
                """
                SELECT action, (details->>'processed')::int
                FROM identity.audit_records
                WHERE action LIKE 'ops.keyrotation.%' AND details->>'kind' = 'fingerprint-key'
                """))
                .GroupBy(row => row.Action, StringComparer.Ordinal)
                .Select(rows => (rows.Key, rows.Key == "ops.keyrotation.completed" ? rows.Single().Processed : rows.Count()))
                .OrderBy(row => row.Key, StringComparer.Ordinal));
    }

    /// <summary>
    /// OPS-SEC-003 AC4, AC6, CONV-DESIGN-003 AC6: two confirmations of the seal at once
    /// each retire with the progress held, so one retires the rotation and the other
    /// finds it retired and is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_TwoSealsAtOnceRetireTheRotationOnceAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Completed(await RecomputedAsync(new FixedTime(Noon), cancellationToken));

        Result<KeyRetirement>[] seals = await Task.WhenAll(
            RetiredAsync(new FixedTime(Noon), cancellationToken),
            RetiredAsync(new FixedTime(Noon), cancellationToken));

        Assert.Equal(
            ErrorCodes.RotationNotReady,
            Assert.Single(seals, seal => seal.Match(_ => false, _ => true)).Match(_ => default, error => error.Code));
        Assert.Equal([1], Retirement(Assert.Single(seals, seal => seal.Match(_ => true, _ => false))).Retired);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM identity.audit_records WHERE action = 'ops.keyrotation.retired'"));
    }

    /// <summary>
    /// OPS-SEC-003 AC6: a stored fingerprint its own value does not compute under its
    /// version is a defect the rotation stops on rather than replaces.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_AFingerprintItsValueDoesNotComputeStopsTheRunAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        await WrittenAsync(subject, Fresh("Altered"));
        byte[] altered = RandomNumberGenerator.GetBytes(Fingerprint.Length);

        await using NpgsqlConnection connection = await database.OpenAsync();
        await connection.ExecuteAsync("UPDATE identity.identifiers SET fingerprint = @altered", new { altered });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await RecomputedAsync(new FixedTime(Noon), TestContext.Current.CancellationToken));

        (int version, byte[] fingerprint) = await connection.QuerySingleAsync<(int, byte[])>(
            "SELECT fingerprint_version, fingerprint FROM identity.identifiers");

        Assert.Equal(1, version);
        Assert.Equal(altered, fingerprint);
    }

    private static KeyRotationProgress Completed(Result<KeyRotationProgress> outcome) => outcome.Match(
        progress => progress,
        error => throw new InvalidOperationException("The rotation was refused: " + error.Code + "."));

    private static KeyRetirement Retirement(Result<KeyRetirement> outcome) => outcome.Match(
        retirement => retirement,
        error => throw new InvalidOperationException("The retirement was refused: " + error.Code + "."));

    private static Error Refusal(Result<KeyRetirement> outcome) => outcome.Match(
        _ => throw new InvalidOperationException("The retirement was not refused."),
        error => error);

    private static byte[] Hashed(string canonical) =>
        Fingerprint.Compute(Encoding.UTF8.GetBytes(canonical), Next);

    // A sign-in opened for an identifier no account holds.
    private Challenge Opened() =>
        Challenge.Open(
            OpaqueToken.Draw(_deployment.Randomness),
            subject: null,
            email: null,
            RandomNumberGenerator.GetBytes(32),
            "a-value-an-assertion-signs",
            Noon,
            TimeSpan.FromMinutes(10));

    private static string Fresh(string person) =>
        person + "." + Guid.NewGuid().ToString("N") + "@Example.COM";

    private static string Canonicalised(string entered) => Parsed(entered).Value;

    private static EmailAddress Parsed(string entered)
    {
        Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));

        return address;
    }

    private static async Task<Guid[]> SubjectsUnderAsync(NpgsqlConnection connection, int version) =>
        [.. await connection.QueryAsync<Guid>(
            "SELECT subject FROM identity.identifiers WHERE fingerprint_version = @version ORDER BY subject",
            new { version })];

    // Waits until the running rotation has committed the given count.
    private static async Task ProcessedAsync(NpgsqlConnection connection, int processed)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        patience.CancelAfter(TimeSpan.FromSeconds(60));

        while (await connection.ExecuteScalarAsync<int?>("SELECT max(processed) FROM identity.key_rotations") != processed)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), patience.Token);
        }
    }

    // The deployment's key ring, holding the given versions of the fingerprint key.
    private KeyRingInMemory Ring(FingerprintKeys fingerprintKeys) => new(_deployment.Keys, fingerprintKeys);

    private IdentifierStore Identifiers(StoreContext context, FingerprintKeys fingerprintKeys) =>
        new(context, Ring(fingerprintKeys), _deployment.Randomness);

    private AuthenticatorStore Authenticators(StoreContext context, FingerprintKeys fingerprintKeys) =>
        new(context, Ring(fingerprintKeys), _deployment.Randomness);

    private MailboxStore Mailboxes(StoreContext context, FingerprintKeys fingerprintKeys) =>
        new(context, _deployment.DataKey(context), Ring(fingerprintKeys), _deployment.Randomness);

    // A value of every column the rotation computes again, written under the previous
    // version as an application holding only that version writes it: an identifier kept
    // and one given up, a provider's link, a mailbox reserved and one held.
    private async Task<Seeded> SeedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId subject = await _deployment.AccountAsync(Noon);
        SubjectId linked = await _deployment.AccountAsync(Noon);
        string kept = Fresh("Kept");
        string given = Fresh("Given");

        await WrittenAsync(subject, kept);
        await WrittenAsync(subject, given);

        await using (StoreContext giving = database.Context())
        {
            IdentifierStore store = Identifiers(giving, Deployment.FingerprintKeys);
            IdentifierSet set = await store.FindBySubjectAsync(subject, cancellationToken);
            Identifier first = set.All.Single(identifier => identifier.Canonical == Canonicalised(kept));
            Identifier second = set.All.Single(identifier => identifier.Canonical == Canonicalised(given));

            set.Verify(first.Id, Noon);
            set.Verify(second.Id, Noon);

            await store.RecordRemovalAsync(
                IdentifierRemoval.Of(set.Remove(second.Id), Noon, Noon.AddHours(72), [7, 3, 9]),
                cancellationToken);
            await store.RecordAsync(set, cancellationToken);
            await giving.SaveChangesAsync(cancellationToken);
        }

        string providerSubject = "provider-" + Guid.NewGuid().ToString("N");

        await using (StoreContext linking = database.Context())
        {
            Assert.True(CredentialLabel.TryParse("Google", out CredentialLabel label));

            await Authenticators(linking, Deployment.FingerprintKeys).LinkAsync(
                Authenticator.Linked(AuthenticatorId.New(TimeProvider.System), linked, Factor.Google, label, Noon),
                providerSubject,
                cancellationToken);
            await linking.SaveChangesAsync(cancellationToken);
        }

        EmailAddress reserved = Parsed("reserved." + Guid.NewGuid().ToString("N") + "@example.test");
        EmailAddress held = Parsed("held." + Guid.NewGuid().ToString("N") + "@example.test");
        var holding = Mailbox.Reserved(held, Noon);

        holding.Hold(subject);

        await using (StoreContext provisioning = database.Context())
        {
            MailboxStore mailboxes = Mailboxes(provisioning, Deployment.FingerprintKeys);

            await mailboxes.AddAsync(Mailbox.Reserved(reserved, Noon), cancellationToken);
            await mailboxes.AddAsync(holding, cancellationToken);
            await provisioning.SaveChangesAsync(cancellationToken);
        }

        return new Seeded(subject, Canonicalised(kept), Canonicalised(given), linked, providerSubject, reserved, held);
    }

    private async Task<string> WrittenAsync(SubjectId subject, string entered)
    {
        await using StoreContext context = database.Context();
        IdentifierStore store = Identifiers(context, Deployment.FingerprintKeys);

        IdentifierSet set = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);
        set.Add(
            Identifier.Email(IdentifierId.New(TimeProvider.System), subject, Parsed(entered), entered, Noon),
            maximum: 5);

        await store.RecordAsync(set, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return entered;
    }

    // A username an erasure holds, as the eraser writes the hold.
    private async Task HeldAsync(string canonical, int version, DateTimeOffset releases)
    {
        await using StoreContext context = database.Context();

        context.UsernameHolds.Add(new UsernameHoldRecord
        {
            Fingerprint = Fingerprint.Compute(Encoding.UTF8.GetBytes(canonical), Deployment.FingerprintKey),
            FingerprintVersion = version,
            HeldFrom = Noon,
            ReleasesAt = releases,
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Result<KeyRotationProgress>> RecomputedAsync(TimeProvider time, CancellationToken cancellationToken)
    {
        await using ServiceProvider services = Composed(time);
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<FingerprintKeyRotation>().RecomputeAsync(cancellationToken);
    }

    private async Task<Result<KeyRetirement>> RetiredAsync(TimeProvider time, CancellationToken cancellationToken)
    {
        await using ServiceProvider services = Composed(time);
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<FingerprintKeyRotation>().RetireAsync(cancellationToken);
    }

    // The rotation as the command composes it, under the maintenance credential and
    // handed the new version as current with the previous one beside it.
    private ServiceProvider Composed(TimeProvider time)
    {
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Options = "-c role=identity_maintenance",
        };
        var services = new ServiceCollection();

        services.AddSingleton(time);
        services.AddSingleton<IKeyRing>(Ring(Rotating));
        services.AddStorageArea(connection.ConnectionString);
        services.AddScoped<IKeyRotationStore>(provider => new KeyRotationStore(
            provider.GetRequiredService<StoreContext>(),
            provider.GetRequiredService<DataConnections>(),
            provider.GetRequiredService<IKeyRing>()));
        services.AddScoped<IFingerprintRotationStore>(provider => new FingerprintRotationStore(
            provider.GetRequiredService<DataConnections>(),
            provider.GetRequiredService<DeploymentDataKeyStore>(),
            provider.GetRequiredService<IKeyRing>()));
        services.AddScoped(provider => new FingerprintKeyRotation(
            provider.GetRequiredService<IKeyRotationStore>(),
            provider.GetRequiredService<IFingerprintRotationStore>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<IPrivacyAudit>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IKeyRing>()));

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed record Seeded(
        SubjectId Subject,
        string Kept,
        string Given,
        SubjectId Linked,
        string ProviderSubject,
        EmailAddress Reserved,
        EmailAddress Held);
}
