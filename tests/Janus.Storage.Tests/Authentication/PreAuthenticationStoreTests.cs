using System;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Sessions;
using Janus.Storage.Authentication.Sessions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What a browser carries before it holds a session, as the
/// <c>preauthentication_sessions</c> table holds it, and the sign-on it has in flight
/// (BFF-CSRF-005a, BFF-SESS-006).
/// </summary>
[Trait("kind", "integration")]
public sealed class PreAuthenticationStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// BFF-SESS-006, OPS-SEC-001: the proof key the token request presents is a
    /// secret this server holds, so it goes down wrapped under the key-encryption key
    /// and comes back only through the store that unwraps it.
    /// </summary>
    [Fact]
    public async Task BFF_SESS_006_TheProofKeyIsAtRestUnderTheKeyEncryptionKeyAsync()
    {
        var secret = OpaqueToken.Of("the-token-this-browser-carries");
        var state = OpaqueToken.Of("the-state-it-was-sent-out-with");
        const string verifier = "a-verifier-of-at-least-forty-three-characters-long";

        await CarriedAsync(secret, new SignOnAttempt(state.Fingerprint(), verifier, "/account"));

        await using StoreContext reading = database.Context();

        PreAuthentication held = Assert.IsType<PreAuthentication>(
            await Store(reading).FindAsync(secret.Fingerprint(), TestContext.Current.CancellationToken));
        SignOnAttempt attempt = Assert.IsType<SignOnAttempt>(held.SignOn);

        Assert.Equal(verifier, attempt.Verifier);
        Assert.Equal("/account", attempt.ReturnTo);
        Assert.Equal(state.Fingerprint(), attempt.StateFingerprint);

        await using NpgsqlConnection connection = await database.OpenAsync();

        byte[] stored = await connection.QuerySingleAsync<byte[]>(
            "SELECT signon_verifier FROM janus.preauthentication_sessions "
                + "WHERE fingerprint = @Fingerprint;",
            new { Fingerprint = secret.Fingerprint() });

        Assert.NotEqual(Encoding.ASCII.GetBytes(verifier), stored);
    }

    /// <summary>
    /// BFF-SESS-006 AC3: a return is judged once, so forgetting the attempt clears
    /// every column of it and leaves the browser its token.
    /// </summary>
    [Fact]
    public async Task BFF_SESS_006_AC3_ForgettingTheAttemptClearsEveryColumnOfItAsync()
    {
        var secret = OpaqueToken.Of("the-token-of-a-browser-that-returned");
        var state = OpaqueToken.Of("the-state-of-a-browser-that-returned");

        await CarriedAsync(
            secret,
            new SignOnAttempt(state.Fingerprint(), "a-verifier-long-enough-to-be-one", "/"));

        await using (StoreContext forgetting = database.Context())
        {
            PreAuthentication held = Assert.IsType<PreAuthentication>(
                await Store(forgetting)
                    .FindAsync(secret.Fingerprint(), TestContext.Current.CancellationToken));

            held.Abandon();

            await Store(forgetting).RecordAsync(held, TestContext.Current.CancellationToken);
            await forgetting.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Null(
            Assert.IsType<PreAuthentication>(
                await Store(reading)
                    .FindAsync(secret.Fingerprint(), TestContext.Current.CancellationToken))
                .SignOn);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private PreAuthenticationStore Store(StoreContext context) => new(context, _deployment.Keys);

    private async Task CarriedAsync(OpaqueToken secret, SignOnAttempt attempt)
    {
        await using StoreContext writing = database.Context();

        var contact = PreAuthentication.Issue(
            secret,
            OpaqueToken.Of("the-token-a-state-change-presents-back"),
            Noon,
            TimeSpan.FromHours(1));

        await Store(writing).AddAsync(contact, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        contact.Carry(attempt);

        await Store(writing).RecordAsync(contact, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
