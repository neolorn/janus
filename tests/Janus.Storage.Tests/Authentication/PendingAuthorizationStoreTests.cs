using System;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Oidc;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Storage.Authentication.Oidc;
using Janus.Storage.Authentication.Sessions;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The authorizations browsers have waiting for their sign-in, as the
/// <c>pending_authorizations</c> table holds them: bound to a browser, carried to a
/// session record, spent once, and ended with what they are bound to (AUTH-SESS-012,
/// D-224, D-226).
/// </summary>
[Trait("kind", "integration")]
public sealed class PendingAuthorizationStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Parameters =
        "{\"client_id\":\"browser-app\",\"state\":\"the-state\",\"login_hint\":\"person@example.test\"}";

    private static readonly DateTimeOffset Noon = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTH-SESS-012 AC10, OPS-SEC-001: what was kept for a browser is carried to the
    /// record its sign-in established, read back as it was kept and spent by the first
    /// take; the parameters, which may name the person, are at rest wrapped.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_012_AC10_AKeptRequestIsCarriedAndSpentOnceAsync()
    {
        byte[] browser = await BrowserAsync("a-browser-that-was-sent-to-sign-in");
        SessionId record = await RecordAsync();

        await KeptAsync(browser, Parameters, Noon);

        bool carried = await WithinAsync(store => store.CarryAsync(browser, record, Noon.AddMinutes(5), default));

        await using NpgsqlConnection connection = await database.OpenAsync();

        byte[] stored = await connection.QuerySingleAsync<byte[]>(
            "SELECT parameters FROM identity.pending_authorizations WHERE session = @Session;",
            new { Session = record.Value });

        bool waits = await WithinAsync(store => store.WaitsAsync(record, Noon.AddMinutes(6), default));
        string? taken = await WithinAsync(store => store.TakeAsync(record, Noon.AddMinutes(6), default));
        string? again = await WithinAsync(store => store.TakeAsync(record, Noon.AddMinutes(6), default));

        Assert.True(carried);
        Assert.DoesNotContain("person@example.test", Encoding.UTF8.GetString(stored), StringComparison.Ordinal);
        Assert.True(waits);
        Assert.Equal(Parameters, taken);
        Assert.Null(again);
    }

    /// <summary>
    /// AUTH-SESS-012 (D-224): a browser has one waiting, a later one replacing it, and
    /// one past its lifetime is neither carried nor taken.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_012_AC10_ALaterRequestReplacesTheEarlierAndALapsedOneIsNotCarriedAsync()
    {
        byte[] browser = await BrowserAsync("a-browser-that-asked-twice");
        byte[] lapsing = await BrowserAsync("a-browser-that-waited-too-long");
        SessionId record = await RecordAsync();
        SessionId other = await RecordAsync();

        await KeptAsync(browser, "{\"state\":\"first\"}", Noon);
        await KeptAsync(browser, "{\"state\":\"later\"}", Noon.AddMinutes(1));
        await KeptAsync(lapsing, Parameters, Noon);

        bool carried = await WithinAsync(store => store.CarryAsync(browser, record, Noon.AddMinutes(2), default));
        bool lapsed = await WithinAsync(store => store.CarryAsync(lapsing, other, Noon.AddMinutes(31), default));
        string? taken = await WithinAsync(store => store.TakeAsync(record, Noon.AddMinutes(3), default));
        string? none = await WithinAsync(store => store.TakeAsync(other, Noon.AddMinutes(31), default));

        Assert.True(carried);
        Assert.False(lapsed);
        Assert.Equal("{\"state\":\"later\"}", taken);
        Assert.Null(none);
    }

    /// <summary>
    /// AUTH-SESS-012 (D-226): what waits ends with the pre-authentication session it is
    /// bound to, and the sweep ends what has lapsed wherever it is bound.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_012_AC10_WhatWaitsEndsWithItsBrowserAndAtItsLifetimeAsync()
    {
        byte[] ended = await BrowserAsync("a-browser-whose-first-contact-ended");
        byte[] lapsing = await BrowserAsync("a-browser-whose-request-lapsed");
        byte[] kept = await BrowserAsync("a-browser-still-within-its-lifetime");
        SessionId record = await RecordAsync();

        await KeptAsync(ended, Parameters, Noon);
        await KeptAsync(lapsing, Parameters, Noon);
        await KeptAsync(kept, Parameters, Noon.AddMinutes(20));
        _ = await WithinAsync(store => store.CarryAsync(kept, record, Noon.AddMinutes(21), default));

        await using (StoreContext removing = database.Context())
        {
            await new PreAuthenticationStore(removing, _deployment.DataKey(removing))
                .RemoveAsync(ended, TestContext.Current.CancellationToken);
            await removing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        int swept = await WithinAsync(store => store.SweepAsync(Noon.AddMinutes(30), default));
        bool waits = await WithinAsync(store => store.WaitsAsync(record, Noon.AddMinutes(30), default));

        await using NpgsqlConnection connection = await database.OpenAsync();

        int left = await connection.QuerySingleAsync<int>(
            "SELECT count(*) FROM identity.pending_authorizations WHERE preauthentication = ANY(@Browsers) OR session = @Session;",
            new { Browsers = new[] { ended, lapsing, kept }, Session = record.Value });

        Assert.True(swept >= 1);
        Assert.True(waits);
        Assert.Equal(1, left);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private PendingAuthorizationStore Store(StoreContext context) => new(context, _deployment.DataKey(context));

    private async Task<TResult> WithinAsync<TResult>(Func<PendingAuthorizationStore, ValueTask<TResult>> operation)
    {
        await using StoreContext context = database.Context();

        TResult result = await operation(Store(context));

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private async Task KeptAsync(byte[] browser, string parameters, DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();

        await Store(writing).KeepAsync(
            PendingAuthorization.Keep(browser, parameters, at, TimeSpan.FromMinutes(30)),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<byte[]> BrowserAsync(string token)
    {
        var secret = OpaqueToken.Of(token + "-" + Guid.NewGuid().ToString("N"));

        await using StoreContext writing = database.Context();

        await new PreAuthenticationStore(writing, _deployment.DataKey(writing)).AddAsync(
            PreAuthentication.Issue(secret, OpaqueToken.Draw(_deployment.Randomness), Noon, TimeSpan.FromDays(1)),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return secret.Fingerprint();
    }

    private async Task<SessionId> RecordAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var record = Session.Begin(
            SessionId.New(TimeProvider.System),
            subject,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            Noon,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        await using StoreContext writing = database.Context();

        await new SessionStore(writing, _deployment.Ring, _deployment.Randomness).AddAsync(
            record,
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return record.Id;
    }
}
