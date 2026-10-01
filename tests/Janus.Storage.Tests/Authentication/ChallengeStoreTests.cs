using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.SignIn;
using Janus.Core;
using Janus.Storage.Authentication.SignIn;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The sign-ins in progress, as the <c>signin_challenges</c> table holds them: the hash
/// of the identifier each was opened with, beside the version of the fingerprint key it
/// was computed under (AUTH-ABUSE-001, OPS-SEC-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class ChallengeStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// AUTH-ABUSE-001 AC1: the hash of the identifier a sign-in was opened with travels
    /// with it, whether or not an account holds the identifier, so a factor refused
    /// against it is counted against the identifier; it is written under the current
    /// version of the fingerprint key.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_001_AC1_TheIdentifierTravelsWithTheSignInAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        byte[] identifier = RandomNumberGenerator.GetBytes(Fingerprint.Length);
        var opened = Challenge.Open(
            OpaqueToken.Draw(_randomness),
            subject: null,
            email: null,
            identifier,
            "a-value-an-assertion-signs",
            Noon,
            TimeSpan.FromMinutes(10));

        await using (StoreContext writing = database.Context())
        {
            await new ChallengeStore(writing, Deployment.Fingerprints).AddAsync(opened, cancellationToken);
            _ = await writing.SaveChangesAsync(cancellationToken);
        }

        await using StoreContext reading = database.Context();
        Challenge found = Assert.IsType<Challenge>(
            await new ChallengeStore(reading, Deployment.Fingerprints).FindAsync(opened.Fingerprint, cancellationToken));

        Assert.Equal(identifier, found.Identifier);
        Assert.Null(found.Subject);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            Deployment.FingerprintKeys.CurrentVersion,
            await connection.QuerySingleAsync<int>(
                "SELECT fingerprint_version FROM identity.signin_challenges WHERE handle = @Handle;",
                new { Handle = opened.Fingerprint }));
    }

    /// <summary>
    /// OPS-SEC-003: every sign-in holds a hash, only beside the version it was computed
    /// under and only at the length a hash has, so the rotation meets no hash it cannot
    /// place and no sign-in escapes the identifier's count.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AnIdentifierIsHeldOnlyBesideItsVersionAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException unversioned = await Assert.ThrowsAsync<PostgresException>(
            async () => await WrittenAsync(connection, RandomNumberGenerator.GetBytes(Fingerprint.Length), version: null));
        PostgresException unhashed = await Assert.ThrowsAsync<PostgresException>(
            async () => await WrittenAsync(connection, identifier: null, version: 1));
        PostgresException neither = await Assert.ThrowsAsync<PostgresException>(
            async () => await WrittenAsync(connection, identifier: null, version: null));
        PostgresException truncated = await Assert.ThrowsAsync<PostgresException>(
            async () => await WrittenAsync(connection, RandomNumberGenerator.GetBytes(16), version: 1));

        Assert.Equal(PostgresErrorCodes.NotNullViolation, unversioned.SqlState);
        Assert.Equal("fingerprint_version", unversioned.ColumnName);
        Assert.Equal(PostgresErrorCodes.NotNullViolation, unhashed.SqlState);
        Assert.Equal("identifier", unhashed.ColumnName);
        Assert.Equal(PostgresErrorCodes.NotNullViolation, neither.SqlState);
        Assert.Equal("ck_signin_challenges_identifier", truncated.ConstraintName);
        Assert.Equal(1, await WrittenAsync(connection, RandomNumberGenerator.GetBytes(Fingerprint.Length), version: 1));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC6: two completions of one sign-in at once read its challenge
    /// under its lock, so the second waits for the first to remove it, finds nothing,
    /// and one session is issued.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC6_TwoCompletionsOfOneSignInAtOnceCompleteOnceAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var opened = Challenge.Open(
            OpaqueToken.Draw(_randomness),
            subject: null,
            email: null,
            RandomNumberGenerator.GetBytes(Fingerprint.Length),
            "a-value-an-assertion-signs",
            Noon,
            TimeSpan.FromMinutes(10));

        await using (StoreContext writing = database.Context())
        {
            await new ChallengeStore(writing, Deployment.Fingerprints).AddAsync(opened, cancellationToken);
            _ = await writing.SaveChangesAsync(cancellationToken);
        }

        bool[] completed = await Task.WhenAll(CompletedAsync(opened), CompletedAsync(opened));

        Assert.Equal(1, completed.Count(answer => answer));
    }

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    // Each completion is its own request, holding the challenge and removing it as the
    // sign-in does around the session it issues.
    private async Task<bool> CompletedAsync(Challenge opened)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        var challenges = new ChallengeStore(context, Deployment.Fingerprints);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        bool held = await challenges.FindForUpdateAsync(opened.Fingerprint, TestContext.Current.CancellationToken)
            is not null;

        if (held)
        {
            await challenges.RemoveAsync(opened.Fingerprint, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return held;
    }

    private static Task<int> WrittenAsync(NpgsqlConnection connection, byte[]? identifier, int? version) =>
        connection.ExecuteAsync(
            """
            INSERT INTO identity.signin_challenges
                (handle, webauthn, created_at, expires_at, presented, identifier, fingerprint_version)
            VALUES (@Handle, 'a-value-an-assertion-signs', @Noon, @Noon, '{}', @Identifier, @Version);
            """,
            new
            {
                Handle = RandomNumberGenerator.GetBytes(Fingerprint.Length),
                Noon,
                Identifier = identifier,
                Version = version,
            });
}
