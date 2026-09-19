using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Sessions;
using Microsoft.EntityFrameworkCore;
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

        await using (JanusDbContext ending = database.Context())
        {
            await Store(ending).EndSpineAsync(
                record.Id,
                Noon + TimeSpan.FromHours(2),
                TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();

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

        await using JanusDbContext reading = database.Context();
        Session read = Assert.IsType<Session>(
            await Store(reading).FindAsync(record.Id, TestContext.Current.CancellationToken));

        Assert.Equal(record.Id, read.Id);
        Assert.Equal(record.Id, read.Spine);
        Assert.Equal(SessionType.Auth, read.Type);
        Assert.Equal(AssuranceLevel.Aal2, read.Attained);
        Assert.True(read.PhishingResistant);
        Assert.Equal(Noon, read.PhishingResistantAt);
        Assert.Equal(Noon, read.CreatedAt);
        Assert.Equal(Noon + TimeSpan.FromDays(1), read.IdleExpiry);
        Assert.Equal(Noon + TimeSpan.FromDays(30), read.AbsoluteExpiry);
        Assert.Equal("198.51.100.7", read.Origin.Address);
        Assert.Equal(new DeviceDescription("Firefox", "Linux"), read.Origin.Device);
        Assert.Equal(new SessionLocation("Cairo", "EG"), read.Origin.Location);
        Assert.Null(read.EndedAt);
        Assert.False(read.SatisfiesEveryGate);
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

        await using JanusDbContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await Store(reading).LiveOfAsync(
                subject,
                Noon + TimeSpan.FromHours(1),
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

        await using JanusDbContext reading = database.Context();
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

        await using (JanusDbContext writing = database.Context())
        {
            await Store(writing).AddAsync(
                record,
                secret.Fingerprint(),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
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

        await using (JanusDbContext writing = database.Context())
        {
            await Store(writing).AddAsync(record, first, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (JanusDbContext rotating = database.Context())
        {
            await Store(rotating).ReplaceSecretAsync(
                record.Id,
                second,
                TestContext.Current.CancellationToken);
            await rotating.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();

        Assert.Null(await Store(reading).FindByFingerprintAsync(
            first,
            TestContext.Current.CancellationToken));
        Assert.NotNull(await Store(reading).FindByFingerprintAsync(
            second,
            TestContext.Current.CancellationToken));
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

        await using JanusDbContext reading = database.Context();
        IReadOnlyList<Session> live = await Store(reading).LiveOfAsync(
            subject,
            Noon + TimeSpan.FromHours(1),
            TestContext.Current.CancellationToken);

        Assert.Equal([standing.Id], [.. Identifiers(live)]);
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

        await using (JanusDbContext changing = database.Context())
        {
            Session held = Assert.IsType<Session>(
                await Store(changing).FindAsync(record.Id, TestContext.Current.CancellationToken));

            held.Touch(
                new SessionOrigin(
                    "203.0.113.9",
                    new DeviceDescription("Safari", "iOS"),
                    new SessionLocation("Alexandria", "EG")),
                Noon + TimeSpan.FromHours(1),
                TimeSpan.FromDays(1));
            held.Present(
                new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
                Noon + TimeSpan.FromHours(1));

            await Store(changing).RecordAsync(held, TestContext.Current.CancellationToken);
            await changing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
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

        await using (JanusDbContext ending = database.Context())
        {
            await Store(ending).EndAccountAsync(
                subject,
                Noon + TimeSpan.FromHours(1),
                TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();

        Assert.Empty(await Store(reading).LiveOfAsync(
            subject,
            Noon + TimeSpan.FromHours(2),
            TestContext.Current.CancellationToken));
        Assert.Single(await Store(reading).LiveOfAsync(
            other,
            Noon + TimeSpan.FromHours(2),
            TestContext.Current.CancellationToken));
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
            new SessionOrigin(
                "198.51.100.7",
                new DeviceDescription("Firefox", "Linux"),
                new SessionLocation("Cairo", "EG")),
            Noon,
            inactivity ?? TimeSpan.FromDays(1),
            absolute ?? TimeSpan.FromDays(30),
            satisfiesEveryGate: false);

    private static Session Derived(Session record, SessionType type) => record.Derive(
        SessionId.New(TimeProvider.System),
        type,
        new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux"), null),
        Noon,
        TimeSpan.FromHours(8));

    private async Task WrittenAsync(params Session[] sessions)
    {
        await using JanusDbContext writing = database.Context();

        foreach (Session session in sessions)
        {
            await Store(writing).AddAsync(
                session,
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                TestContext.Current.CancellationToken);
        }

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private SessionStore Store(JanusDbContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);
}
