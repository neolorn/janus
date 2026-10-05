using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The session record as the <c>sessions</c> table holds it: the spine every credential
/// derives from, the durable copy nothing caches over, and a place held under the
/// person's key (AUTH-SESS-001, AUTH-SESS-003, AUTH-SESS-013).
/// </summary>
[Trait("kind", "integration")]
public sealed class SessionStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTH-SESS-001 AC1: ending the record ends the per-app session and the token
    /// standing on it, in the one statement the request cycle runs.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_001_AC1_EndingTheRecordEndsWhatDerivesFromItAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session record = Record(subject);
        Session application = Derived(record, SessionType.PerApp);
        Session token = Derived(record, SessionType.OidcToken);

        await WrittenAsync(record, application, token);

        await using (StoreContext ending = database.Context())
        {
            await Store(ending).EndSpineAsync(
                record.Id,
                Noon + TimeSpan.FromHours(2),
                TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Empty(await Store(reading).LiveOfAsync(
            subject,
            Noon + TimeSpan.FromHours(3),
            TestContext.Current.CancellationToken));

        foreach (Session held in new[] { record, application, token })
        {
            Session ended = Assert.IsType<Session>(
                await Store(reading).FindAsync(held.Id, TestContext.Current.CancellationToken));

            Assert.Equal(Noon + TimeSpan.FromHours(2), ended.EndedAt);
        }
    }

    /// <summary>
    /// AUTH-SESS-001 AC2: the record is read from PostgreSQL and from nothing else, so
    /// a context that has cached none of it reads every field back as it was written.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_001_AC2_TheRecordReloadsFromTheDurableStoreAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session record = Record(subject, phishingResistant: true);

        await WrittenAsync(record);

        await using StoreContext reading = database.Context();
        Session read = Assert.IsType<Session>(
            await Store(reading).FindAsync(record.Id, TestContext.Current.CancellationToken));

        Assert.Equal(record.Id, read.Id);
        Assert.Equal(record.Id, read.Spine);
        Assert.Equal(SessionType.Auth, read.Type);
        Assert.Equal(AssuranceLevel.Aal2, read.Attained);
        Assert.True(read.PhishingResistant);
        Assert.Equal(
            (Noon, (DateTimeOffset?)Noon, (DateTimeOffset?)Noon, null, (DateTimeOffset?)Noon),
            (read.DelegatedAt, read.Aal1At, read.Aal2At, read.Aal3At, read.PhishingResistantAt));
        Assert.Equal(Noon, read.CreatedAt);
        Assert.Equal(Noon + TimeSpan.FromDays(1), read.IdleExpiry);
        Assert.Equal(Noon + TimeSpan.FromDays(30), read.AbsoluteExpiry);
        Assert.Equal("198.51.100.7", read.Origin.Address);
        Assert.Equal(new DeviceDescription("Firefox", "Linux"), read.Origin.Device);
        Assert.Equal(new SessionLocation("Cairo", "EG"), read.Origin.Location);
        Assert.Null(read.EndedAt);
        Assert.False(read.SatisfiesEveryGate);
        Assert.Null(read.BreakGlassReason);
    }

    /// <summary>
    /// OPS-BOOT-002 AC10: the session the break-glass credential opens keeps the reason
    /// given at its use, and what derives from it keeps the same, read back from the
    /// durable store.
    /// </summary>
    [Fact]
    public async Task OPS_BOOT_002_AC10_TheSessionKeepsTheReasonGivenAtItsUseAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var record = Session.Begin(
            SessionId.New(TimeProvider.System),
            subject,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            Noon,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1),
            breakGlassReason: "The operator cannot be reached.");
        Session derived = Derived(record, SessionType.PerApp);

        await WrittenAsync(record, derived);

        await using StoreContext reading = database.Context();
        Session read = Assert.IsType<Session>(
            await Store(reading).FindAsync(record.Id, TestContext.Current.CancellationToken));
        Session readDerived = Assert.IsType<Session>(
            await Store(reading).FindAsync(derived.Id, TestContext.Current.CancellationToken));

        Assert.True(read.SatisfiesEveryGate);
        Assert.Equal("The operator cannot be reached.", read.BreakGlassReason);
        Assert.Equal("The operator cannot be reached.", readDerived.BreakGlassReason);
    }

    /// <summary>
    /// REG-SESS-008 (D-166, 145): the session a registration's terms step established
    /// keeps the client the registration captured, read back from the durable store, and
    /// what derives from it keeps none.
    /// </summary>
    [Fact]
    public async Task REG_SESS_008_TheSessionKeepsTheClientTheRegistrationCapturedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session record = Record(subject);

        record.Capture("web");

        Session derived = Derived(record, SessionType.PerApp);

        await WrittenAsync(record, derived);

        await using StoreContext reading = database.Context();
        Session read = Assert.IsType<Session>(
            await Store(reading).FindAsync(record.Id, TestContext.Current.CancellationToken));
        Session readDerived = Assert.IsType<Session>(
            await Store(reading).FindAsync(derived.Id, TestContext.Current.CancellationToken));

        Assert.Equal("web", read.Client);
        Assert.Null(readDerived.Client);
    }

    /// <summary>
    /// AUTH-SESS-013 AC4: the address and the city are held under the person's key, so
    /// destroying that key leaves the location of every session unreadable.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_013_AC4_TheLocationIsUnreadableAfterErasureAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await WrittenAsync(Record(subject));
        await _deployment.EraseAsync(subject);

        await using StoreContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await Store(reading).LiveOfAsync(
                subject,
                Noon + TimeSpan.FromHours(1),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RET-005 AC1: the location is kept with the session record and no longer.
    /// Erasing the person leaves it unreadable, and the sweep that takes the expired
    /// session takes the location with it.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_005_AC1_TheLocationIsUnreadableAfterErasureAndGoneWithTheSessionAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session record = Record(subject, absolute: TimeSpan.FromDays(1));

        await WrittenAsync(record);
        await _deployment.EraseAsync(subject);

        await using (StoreContext reading = database.Context())
        {
            SessionRecord stored = await reading.Sessions
                .SingleAsync(session => session.Id == record.Id, TestContext.Current.CancellationToken);

            Assert.NotEmpty(stored.OriginPlace);

            await Assert.ThrowsAsync<CryptographicException>(async () =>
                await Store(reading).FindAsync(record.Id, TestContext.Current.CancellationToken));
        }

        await using (StoreContext sweeping = database.Context())
        {
            _ = await Store(sweeping).SweepAsync(
                Noon + TimeSpan.FromDays(2),
                TestContext.Current.CancellationToken);
        }

        await using StoreContext after = database.Context();

        Assert.False(await after.Sessions.AnyAsync(
            session => session.Id == record.Id,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-SESS-013 AC4: neither the address nor the city stands in the table in
    /// plain, so a dump alone says nothing about where the person signed in from.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_013_AC4_TheLocationIsNotReadableFromTheTableAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session record = Record(subject);

        await WrittenAsync(record);

        await using StoreContext reading = database.Context();
        SessionRecord stored = await reading.Sessions
            .SingleAsync(session => session.Id == record.Id, TestContext.Current.CancellationToken);

        foreach (byte[] place in new[] { stored.OriginPlace, stored.LastSeenPlace })
        {
            Assert.Equal(PersonalDataFormat.Marker, place[0]);
            Assert.Equal(-1, place.AsSpan().IndexOf(Encoding.UTF8.GetBytes("198.51.100.7")));
            Assert.Equal(-1, place.AsSpan().IndexOf(Encoding.UTF8.GetBytes("Cairo")));
        }

        // AUTH-SESS-013 AC2: the device description is not a location and is shown to
        // the person whatever the key does, so it is not held under it.
        Assert.Equal("Firefox", stored.OriginBrowser);
        Assert.Equal("Linux", stored.OriginOs);
    }

    /// <summary>
    /// AUTH-SESS-003 AC3: the row carries what the cookie fingerprints to and never
    /// the cookie, and the session is resolved from that fingerprint alone.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_003_AC3_TheRowCarriesTheFingerprintAndNotTheSecretAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session record = Record(subject);
        var secret = OpaqueToken.Draw(_deployment.Randomness);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(
                record,
                secret.Fingerprint(),
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        SessionRecord stored = await reading.Sessions
            .SingleAsync(session => session.Id == record.Id, TestContext.Current.CancellationToken);

        Assert.Equal(Fingerprint.Length, stored.SecretFingerprint.Length);
        Assert.Equal(
            -1,
            stored.SecretFingerprint.AsSpan().IndexOf(Encoding.UTF8.GetBytes(secret.Value)));

        Session found = Assert.IsType<Session>(
            await Store(reading).FindByFingerprintAsync(
                secret.Fingerprint(),
                TestContext.Current.CancellationToken));

        Assert.Equal(record.Id, found.Id);
    }

    /// <summary>
    /// The secret a session answers to is replaced rather than added to, so the value
    /// rotation issued is the only one that resolves the session afterwards.
    /// </summary>
    [Fact]
    public async Task ReplaceSecretAsync_AfterRotation_OnlyTheNewFingerprintResolvesAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session record = Record(subject);
        byte[] first = OpaqueToken.Draw(_deployment.Randomness).Fingerprint();
        byte[] second = OpaqueToken.Draw(_deployment.Randomness).Fingerprint();

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(
                record,
                first,
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext rotating = database.Context())
        {
            await Store(rotating).ReplaceSecretAsync(
                record.Id,
                second,
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                TestContext.Current.CancellationToken);
            await rotating.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Null(await Store(reading).FindByFingerprintAsync(
            first,
            TestContext.Current.CancellationToken));
        Assert.NotNull(await Store(reading).FindByFingerprintAsync(
            second,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// BFF-CSRF-001 and BFF-CSRF-006: the synchronizer token is held as a fingerprint
    /// on the session row, and rotation replaces it with the one issued alongside the
    /// new secret rather than leaving the previous token usable.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_001_AC1_TheRowCarriesTheTokenBoundToTheSessionAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session record = Record(subject);
        var first = OpaqueToken.Draw(_deployment.Randomness);
        var second = OpaqueToken.Draw(_deployment.Randomness);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(
                record,
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                first.Fingerprint(),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext bound = database.Context())
        {
            SessionRecord stored = await bound.Sessions
                .SingleAsync(session => session.Id == record.Id, TestContext.Current.CancellationToken);

            Assert.Equal(first.Fingerprint(), stored.CsrfFingerprint);
            Assert.Equal(
                -1,
                stored.CsrfFingerprint.AsSpan().IndexOf(Encoding.UTF8.GetBytes(first.Value)));
        }

        await using (StoreContext rotating = database.Context())
        {
            await Store(rotating).ReplaceSecretAsync(
                record.Id,
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                second.Fingerprint(),
                TestContext.Current.CancellationToken);
            await rotating.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            second.Fingerprint(),
            await Store(reading).CsrfFingerprintAsync(record.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A session that lapsed is not live, whichever of its two clocks ran out, and
    /// what the account is shown is what has not.
    /// </summary>
    [Fact]
    public async Task LiveOfAsync_SessionsThatLapsed_AreNotListedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session standing = Record(subject);
        Session idle = Record(subject, inactivity: TimeSpan.FromMinutes(5));
        Session absolute = Record(subject, absolute: TimeSpan.FromMinutes(10));

        await WrittenAsync(standing, idle, absolute);

        await using StoreContext reading = database.Context();
        IReadOnlyList<Session> live = await Store(reading).LiveOfAsync(
            subject,
            Noon + TimeSpan.FromHours(1),
            TestContext.Current.CancellationToken);

        Assert.Equal([standing.Id], [.. Identifiers(live)]);
    }

    /// <summary>
    /// OPS-ALERT-007: where a session's city lies is kept with the place under the
    /// person's key and reads back as written, and a place written without it reads
    /// back without it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_007_WhereACityLiesReadsBackAsWrittenAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session record = Record(subject);

        record.Touch(
            new SessionOrigin("203.0.113.9", new DeviceDescription("Safari", "iOS"))
            {
                Location = new SessionLocation("Alexandria", "EG"),
                Coordinates = new Coordinates(31.2001, 29.9187),
            },
            Noon,
            TimeSpan.FromDays(1));

        await WrittenAsync(record);

        await using StoreContext reading = database.Context();
        Session read = Assert.IsType<Session>(
            await Store(reading).FindAsync(record.Id, TestContext.Current.CancellationToken));

        Assert.Equal(new Coordinates(31.2001, 29.9187), read.LastSeen.Coordinates);
        Assert.Null(read.Origin.Coordinates);
    }

    /// <summary>
    /// What the session did is carried onto its row: where it was last used, what it
    /// has since reached, and the idle clock the use refreshed.
    /// </summary>
    [Fact]
    public async Task RecordAsync_ASessionUsedAndStepped_CarriesBothOntoTheRowAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session record = Record(subject);

        await WrittenAsync(record);

        await using (StoreContext changing = database.Context())
        {
            Session held = Assert.IsType<Session>(
                await Store(changing).FindAsync(record.Id, TestContext.Current.CancellationToken));

            held.Touch(
                new SessionOrigin("203.0.113.9", new DeviceDescription("Safari", "iOS")) { Location = new SessionLocation("Alexandria", "EG") },
                Noon + TimeSpan.FromHours(1),
                TimeSpan.FromDays(1));
            held.Present(
                new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
                Noon + TimeSpan.FromHours(1));

            await Store(changing).RecordAsync(held, TestContext.Current.CancellationToken);
            await changing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        Session read = Assert.IsType<Session>(
            await Store(reading).FindAsync(record.Id, TestContext.Current.CancellationToken));

        Assert.Equal("203.0.113.9", read.LastSeen.Address);
        Assert.Equal(new SessionLocation("Alexandria", "EG"), read.LastSeen.Location);
        Assert.Equal("198.51.100.7", read.Origin.Address);
        Assert.Equal(AssuranceLevel.Aal2, read.Attained);
        Assert.True(read.PhishingResistant);
        Assert.Equal(Noon + TimeSpan.FromHours(1) + TimeSpan.FromDays(1), read.IdleExpiry);
    }

    /// <summary>
    /// AUTH-SESS-001 AC3: a bare password presented under a session that reached
    /// <c>aal2</c> earlier is carried onto the row as the instant of <c>aal1</c> alone;
    /// the instants of <c>aal2</c> and of phishing resistance read back as they were
    /// reached, and the level the session holds is still the highest it has reached.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_001_AC3_ABarePasswordIsCarriedOntoTheRowAsTheInstantOfAal1AloneAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session record = Record(subject, phishingResistant: true);
        DateTimeOffset later = Noon + TimeSpan.FromHours(1);

        await WrittenAsync(record);

        await using (StoreContext changing = database.Context())
        {
            Session held = Assert.IsType<Session>(
                await Store(changing).FindAsync(record.Id, TestContext.Current.CancellationToken));

            held.Present(new Assurance(AssuranceLevel.Aal1, PhishingResistant: false), later);

            await Store(changing).RecordAsync(held, TestContext.Current.CancellationToken);
            await changing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        Session read = Assert.IsType<Session>(
            await Store(reading).FindAsync(record.Id, TestContext.Current.CancellationToken));

        Assert.Equal(
            (later, (DateTimeOffset?)later, (DateTimeOffset?)Noon, null, (DateTimeOffset?)Noon),
            (read.DelegatedAt, read.Aal1At, read.Aal2At, read.Aal3At, read.PhishingResistantAt));
        Assert.Equal((AssuranceLevel.Aal2, true), (read.Attained, read.PhishingResistant));
    }

    /// <summary>
    /// AUTH-SESS-001 (D-191): a session recorded while the row kept one level and one
    /// instant is carried over with that instant as the instant of each level up to the
    /// one it holds, and of no level above it; the instant it last reached phishing
    /// resistance stands as the row kept it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_SESS_001_ASessionRecordedBeforeIsCarriedOverWithItsInstantAtEachLevelItHoldsAsync()
    {
        string moved = await database.CreateDatabaseAsync("session_levels");
        string carrying;

        await using (StoreContext migrating = DatabaseFixture.Context(moved))
        {
            string[] declared = [.. migrating.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal)];

            carrying = declared.Single(migration =>
                migration.EndsWith("_" + nameof(KeepTheInstantASessionLastReachedEachLevel), StringComparison.Ordinal));

            await migrating.GetService<IMigrator>().MigrateAsync(
                declared[Array.IndexOf(declared, carrying) - 1],
                TestContext.Current.CancellationToken);
        }

        await using var connection = new NpgsqlConnection(moved);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var values = new
        {
            subject = Subjects.New().Value,
            social = Guid.CreateVersion7(),
            single = Guid.CreateVersion7(),
            strong = Guid.CreateVersion7(),
            first = new byte[] { 1 },
            second = new byte[] { 2 },
            third = new byte[] { 3 },
            place = Array.Empty<byte>(),
            at = Noon,
            later = Noon.AddHours(1),
            expiry = Noon.AddDays(1),
        };

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.accounts (subject, state, created_at)
            VALUES (@subject, 'active', @at);
            INSERT INTO identity.sessions
                (id, spine, type, subject, secret_fingerprint, created_at, last_seen_at,
                 attained, attained_at, phishing_resistant, phishing_resistant_at,
                 origin_browser, origin_os, origin_place, last_seen_browser, last_seen_os, last_seen_place,
                 idle_expiry, absolute_expiry, satisfies_every_gate)
            VALUES
                (@social, @social, 'auth', @subject, @first, @at, @at,
                 'delegated', @at, FALSE, NULL, '', '', @place, '', '', @place, @expiry, @expiry, FALSE),
                (@single, @single, 'auth', @subject, @second, @at, @at,
                 'aal1', @later, FALSE, NULL, '', '', @place, '', '', @place, @expiry, @expiry, FALSE),
                (@strong, @strong, 'auth', @subject, @third, @at, @at,
                 'aal2', @later, TRUE, @at, '', '', @place, '', '', @place, @expiry, @expiry, FALSE);
            """,
            values);

        await using (StoreContext migrating = DatabaseFixture.Context(moved))
        {
            await migrating.GetService<IMigrator>().MigrateAsync(carrying, TestContext.Current.CancellationToken);
        }

        DateTimeOffset later = values.later;

        Assert.Equal((Noon, null, null, null, null), await CarriedAsync(values.social));
        Assert.Equal((later, later, null, null, null), await CarriedAsync(values.single));
        Assert.Equal((later, later, later, null, Noon), await CarriedAsync(values.strong));

        async Task<(DateTimeOffset, DateTimeOffset?, DateTimeOffset?, DateTimeOffset?, DateTimeOffset?)> CarriedAsync(Guid id)
        {
            (DateTime Lowest, DateTime? Aal1, DateTime? Aal2, DateTime? Aal3, DateTime? Resistant) row =
                await connection.QuerySingleAsync<(DateTime, DateTime?, DateTime?, DateTime?, DateTime?)>(
                    "SELECT delegated_at, aal1_at, aal2_at, aal3_at, phishing_resistant_at FROM identity.sessions WHERE id = @id",
                    new { id });

            return (Read(row.Lowest), Instant(row.Aal1), Instant(row.Aal2), Instant(row.Aal3), Instant(row.Resistant));
        }

        static DateTimeOffset Read(DateTime instant) => new(DateTime.SpecifyKind(instant, DateTimeKind.Utc));

        static DateTimeOffset? Instant(DateTime? instant) => instant is DateTime read ? Read(read) : null;
    }

    /// <summary>
    /// Ending an account's sessions leaves another account's standing, which is what
    /// makes a suspension an operation on one person and not on the deployment.
    /// </summary>
    [Fact]
    public async Task EndAccountAsync_OneAccount_LeavesAnotherAccountStandingAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        SubjectId other = await _deployment.AccountAsync(Noon);

        await WrittenAsync(Record(subject));
        await WrittenAsync(Record(other));

        await using (StoreContext ending = database.Context())
        {
            await Store(ending).EndAccountAsync(
                subject,
                Noon + TimeSpan.FromHours(1),
                TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Empty(await Store(reading).LiveOfAsync(
            subject,
            Noon + TimeSpan.FromHours(2),
            TestContext.Current.CancellationToken));
        Assert.Single(await Store(reading).LiveOfAsync(
            other,
            Noon + TimeSpan.FromHours(2),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-SESS-009 AC5, AC6, AUTH-SESS-001: a downgrade is written on every session of
    /// the account that has not ended, as the instant it was made, and on no other
    /// account's; what each session attained reads back as it was reached, and an ended
    /// session is left alone.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_009_AC5_ADowngradeIsWrittenOnEveryStandingSessionOfTheAccountAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        SubjectId other = await _deployment.AccountAsync(Noon);
        Session first = Record(subject);
        Session second = Record(subject);
        Session ended = Record(subject);
        Session elsewhere = Record(other);
        DateTimeOffset tightened = Noon + TimeSpan.FromMinutes(1);

        ended.End(Noon);

        await WrittenAsync(first, second, ended);
        await WrittenAsync(elsewhere);

        int downgraded;

        await using (StoreContext writing = database.Context())
        {
            downgraded = await Store(writing).DowngradeAsync(
                subject,
                tightened,
                TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        Session? held = await Store(reading).FindAsync(first.Id, TestContext.Current.CancellationToken);
        Session? another = await Store(reading).FindAsync(second.Id, TestContext.Current.CancellationToken);
        Session? left = await Store(reading).FindAsync(ended.Id, TestContext.Current.CancellationToken);
        Session? untouched = await Store(reading).FindAsync(elsewhere.Id, TestContext.Current.CancellationToken);

        Assert.Equal(2, downgraded);
        Assert.Equal(
            [tightened, tightened, null, null],
            new[] { held?.DowngradedAt, another?.DowngradedAt, left?.DowngradedAt, untouched?.DowngradedAt });
        Assert.Equal(
            (first.Attained, first.DelegatedAt, first.Aal1At, first.Aal2At, first.Aal3At, first.PhishingResistantAt),
            (held?.Attained, held?.DelegatedAt, held?.Aal1At, held?.Aal2At, held?.Aal3At, held?.PhishingResistantAt));
        Assert.False(held?.Counts(first.DelegatedAt));
    }

    /// <summary>
    /// AUTH-KEY-003 AC1: a session that has passed its absolute expiry is taken by the
    /// sweep, which is one call and no person's task; one that has not is left.
    /// </summary>
    [Fact]
    public async Task AUTH_KEY_003_AC1_TheSweepTakesWhatHasPassedItsAbsoluteExpiryAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session expired = Record(subject, absolute: TimeSpan.FromDays(1));
        Session live = Record(subject, absolute: TimeSpan.FromDays(30));

        await using (StoreContext clearing = database.Context())
        {
            // The rows the other tests of this class left are taken first, so what the
            // sweep below counts is this test's own expired session and nothing else.
            _ = await Store(clearing).SweepAsync(
                Noon + TimeSpan.FromDays(2),
                TestContext.Current.CancellationToken);
        }

        await WrittenAsync(expired, live);

        await using StoreContext sweeping = database.Context();

        Assert.Equal(
            1,
            await Store(sweeping).SweepAsync(
                Noon + TimeSpan.FromDays(2),
                TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();

        Assert.Null(await Store(reading).FindAsync(expired.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(await Store(reading).FindAsync(live.Id, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static IEnumerable<SessionId> Identifiers(IReadOnlyList<Session> sessions)
    {
        foreach (Session session in sessions)
        {
            yield return session.Id;
        }
    }

    private static Session Record(
        SubjectId subject,
        bool phishingResistant = false,
        TimeSpan? inactivity = null,
        TimeSpan? absolute = null) =>
        Session.Begin(
            SessionId.New(TimeProvider.System),
            subject,
            new Assurance(
                phishingResistant ? AssuranceLevel.Aal2 : AssuranceLevel.Aal1,
                phishingResistant),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")) { Location = new SessionLocation("Cairo", "EG") },
            Noon,
            inactivity ?? TimeSpan.FromDays(1),
            absolute ?? TimeSpan.FromDays(30),
            breakGlassReason: null);

    private static Session Derived(Session record, SessionType type) => record.Derive(
        SessionId.New(TimeProvider.System),
        type,
        new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
        Noon,
        TimeSpan.FromHours(8));

    private async Task WrittenAsync(params Session[] sessions)
    {
        await using StoreContext writing = database.Context();

        foreach (Session session in sessions)
        {
            await Store(writing).AddAsync(
                session,
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                TestContext.Current.CancellationToken);
        }

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private SessionStore Store(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);
}
