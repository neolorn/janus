using System;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Credentials;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Storage.Authentication.Credentials;
using Janus.Storage.Authentication.Sessions;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The round trips to social providers browsers have in flight, as the
/// <c>provider_attempts</c> table holds them (IDN-LIFE-012, REG-IDENT-008,
/// BFF-CSRF-005a, OPS-SEC-001).
/// </summary>
[Trait("kind", "integration")]
public sealed class ProviderAttemptStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Verifier = "a-verifier-of-at-least-forty-three-characters-long";

    private static readonly DateTimeOffset Noon = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// IDN-LIFE-012 and OPS-SEC-001: the proof key the exchange presents is a secret
    /// this server holds, so it goes down wrapped under the key-encryption key and comes
    /// back only through the store; taking the round trip removes it.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_012_TheProofKeyIsAtRestUnderTheKeyEncryptionKeyAsync()
    {
        ProviderBinding browser = await ContactedAsync("the-token-a-browser-carries-to-google");

        await BoundAsync(browser, Attempt("the-state-it-was-sent-out-with", Verifier));

        await using (NpgsqlConnection connection = await database.OpenAsync())
        {
            byte[] stored = await connection.QuerySingleAsync<byte[]>(
                "SELECT verifier FROM identity.provider_attempts WHERE preauthentication = @Binding;",
                new { Binding = browser.PreAuthentication });

            Assert.NotEqual(Encoding.ASCII.GetBytes(Verifier), stored);
        }

        ProviderAttempt taken = Assert.IsType<ProviderAttempt>(await TakenAsync(browser));

        Assert.Equal(Verifier, taken.Verifier);
        Assert.Equal(Factor.Google, taken.Provider);
        Assert.Equal(ProviderIntent.Register, taken.Intent);
        Assert.Equal("/register", taken.ReturnTo);
        Assert.Null(await TakenAsync(browser));
    }

    /// <summary>
    /// BFF-CSRF-005a: a browser has one round trip in flight, so a second start takes
    /// the place of the first, and another browser's binding finds neither.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_005a_ABrowserHasOneRoundTripInFlightAsync()
    {
        ProviderBinding browser = await ContactedAsync("the-token-of-a-browser-that-started-twice");
        ProviderBinding another = await ContactedAsync("the-token-of-a-browser-that-started-none");

        await BoundAsync(browser, Attempt("the-first-state", Verifier));
        await BoundAsync(browser, Attempt("the-second-state", verifier: null));

        Assert.Null(await TakenAsync(another));

        ProviderAttempt taken = Assert.IsType<ProviderAttempt>(await TakenAsync(browser));

        Assert.Equal(OpaqueToken.Of("the-second-state").Fingerprint(), taken.StateFingerprint);
        Assert.Null(taken.Verifier);
    }

    /// <summary>
    /// IDN-LIFE-012: a round trip lives no longer than what it is bound to, so ending
    /// the pre-authentication session ends it.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_012_ARoundTripGoesWithWhatItIsBoundToAsync()
    {
        ProviderBinding browser = await ContactedAsync("the-token-of-a-browser-that-went-away");

        await BoundAsync(browser, Attempt("a-state-nobody-returns-with", Verifier));

        await using NpgsqlConnection connection = await database.OpenAsync();

        _ = await connection.ExecuteAsync(
            "DELETE FROM identity.preauthentication_sessions WHERE fingerprint = @Binding;",
            new { Binding = browser.PreAuthentication });

        Assert.Equal(
            0,
            await connection.QuerySingleAsync<int>(
                "SELECT count(*)::int FROM identity.provider_attempts WHERE preauthentication = @Binding;",
                new { Binding = browser.PreAuthentication }));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static ProviderAttempt Attempt(string state, string? verifier) =>
        new(
            Factor.Google,
            ProviderIntent.Register,
            OpaqueToken.Of(state).Fingerprint(),
            OpaqueToken.Of("a-nonce-for-" + state).Fingerprint(),
            verifier,
            "/register");

    private ProviderAttemptStore Store(StoreContext context) =>
        new(context, _deployment.Keys, TimeProvider.System);

    private async Task<ProviderBinding> ContactedAsync(string token)
    {
        var secret = OpaqueToken.Of(token);

        await using StoreContext writing = database.Context();

        await new PreAuthenticationStore(writing, _deployment.Keys).AddAsync(
            PreAuthentication.Issue(
                secret,
                OpaqueToken.Of("the-csrf-token-of-" + token),
                Noon,
                TimeSpan.FromHours(1)),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return ProviderBinding.Before(secret.Fingerprint());
    }

    private async Task BoundAsync(ProviderBinding browser, ProviderAttempt attempt)
    {
        await using StoreContext writing = database.Context();

        await Store(writing).BindAsync(browser, attempt, Noon, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ProviderAttempt?> TakenAsync(ProviderBinding browser)
    {
        await using StoreContext taking = database.Context();

        ProviderAttempt? taken = await Store(taking).TakeAsync(browser, TestContext.Current.CancellationToken);

        await taking.SaveChangesAsync(TestContext.Current.CancellationToken);

        return taken;
    }
}
