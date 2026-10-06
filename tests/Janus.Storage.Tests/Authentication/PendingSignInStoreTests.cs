using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.SignIn;
using Janus.Core;
using Janus.Storage.Authentication.SignIn;
using Janus.Storage.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The sign-in links and codes that have gone out, over the <c>signin_links</c> table
/// (AUTH-FACT-003, AUTH-FACT-004).
/// </summary>
[Trait("kind", "integration")]
public sealed class PendingSignInStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Pending =
        """
        INSERT INTO identity.signin_links
            (token, subject, factor, enc_code, issued_at, expires_at, wrong_attempts, credential)
        VALUES (@token, @subject, @factor, @code, @at, @expiry, 0, @credential);
        """;

    private static readonly DateTimeOffset Noon = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTH-FACT-004 AC4: a pending sign-in held for a try is not read by a second try
    /// until the first commits, and the second then decides on what the first left.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC4_APendingSignInHeldForATryIsReadByTheNextOnlyAfterItAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var token = OpaqueToken.Draw(_deployment.Randomness);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).ReplaceAsync(
                PendingSignIn.Issue(token, subject, Factor.EmailLink, null, null, "123456", null, null, Noon, TimeSpan.FromMinutes(15)),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext first = database.Context();
        await using var firstWork = new UnitOfWork(first);
        await using StoreContext second = database.Context();
        await using var secondWork = new UnitOfWork(second);

        _ = await firstWork.BeginAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(await Store(first).FindForUpdateAsync(token.Fingerprint(), TestContext.Current.CancellationToken));

        _ = await secondWork.BeginAsync(TestContext.Current.CancellationToken);
        Task<PendingSignIn?> waiting = Store(second)
            .FindForUpdateAsync(token.Fingerprint(), TestContext.Current.CancellationToken)
            .AsTask();

        Assert.NotSame(
            waiting,
            await Task.WhenAny(waiting, Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken)));

        await Store(first).RemoveAsync(token.Fingerprint(), TestContext.Current.CancellationToken);
        _ = await firstWork.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Null(await waiting);
    }

    /// <summary>
    /// AUTH-FACT-002 AC4: a second step's code is read back bound to the sign-in or
    /// step-up it was issued for, and answers that one and no other.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC4_ASecondStepCodeIsReadBackBoundToItsChallengeAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var token = OpaqueToken.Draw(_deployment.Randomness);
        byte[] challenge = OpaqueToken.Draw(_deployment.Randomness).Fingerprint();
        var credential = new AuthenticatorId(Guid.CreateVersion7());

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).ReplaceAsync(
                PendingSignIn.Issue(token, subject, Factor.PhoneCode, null, credential, "123456", null, challenge, Noon, TimeSpan.FromMinutes(10)),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        PendingSignIn? read = await Store(reading).FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.True(read.Answers(challenge));
        Assert.False(read.Answers(OpaqueToken.Draw(_deployment.Randomness).Fingerprint()));
    }

    /// <summary>
    /// AUTH-FACT-004 AC7: a second step's code is read back naming the credential it was
    /// issued for, whether or not that credential still stands, and a link or code of
    /// any other entry names none.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC7_ASecondStepCodeIsReadBackNamingTheCredentialItWasIssuedForAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        byte[] challenge = OpaqueToken.Draw(_deployment.Randomness).Fingerprint();
        var credential = new AuthenticatorId(Guid.CreateVersion7());

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).ReplaceAsync(
                PendingSignIn.Issue(
                    OpaqueToken.Draw(_deployment.Randomness),
                    subject,
                    Factor.PhoneCode,
                    null,
                    credential,
                    "123456",
                    null,
                    challenge,
                    Noon,
                    TimeSpan.FromMinutes(10)),
                TestContext.Current.CancellationToken);
            await Store(writing).ReplaceAsync(
                PendingSignIn.Issue(
                    OpaqueToken.Draw(_deployment.Randomness),
                    subject,
                    Factor.PhoneLink,
                    null,
                    null,
                    "654321",
                    null,
                    null,
                    Noon,
                    TimeSpan.FromMinutes(15)),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        PendingSignIn? code = await Store(reading).FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken);
        PendingSignIn? link = await Store(reading).FindAsync(subject, Factor.PhoneLink, TestContext.Current.CancellationToken);

        Assert.Equal(credential, code?.Credential);
        Assert.NotNull(link);
        Assert.Null(link.Credential);
    }

    /// <summary>
    /// AUTH-FACT-004: the database refuses a second step's code that names no
    /// credential, and a link or code of any other entry that names one.
    /// </summary>
    /// <param name="factor">The entry the row stands for.</param>
    /// <param name="names">Whether the row names a credential.</param>
    [Theory]
    [InlineData("phoneCode", false)]
    [InlineData("phoneLink", true)]
    [InlineData("emailCode", true)]
    public async Task AUTH_FACT_004_OnlyASecondStepCodeNamesACredentialAndEveryOneDoesAsync(string factor, bool names)
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            Pending,
            new
            {
                token = OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                subject = subject.Value,
                factor,
                code = new byte[] { 1 },
                at = Noon,
                expiry = Noon.AddMinutes(10),
                credential = names ? Guid.CreateVersion7() : (Guid?)null,
            }));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
        Assert.Equal("ck_signin_links_credential", refused.ConstraintName);
    }

    /// <summary>
    /// AUTH-FACT-004 (D-192): the migration that makes a second step's code name its
    /// credential carries no such code pending when it runs: each is removed, since it
    /// names none, and the person asks again. A link or code of any other entry stays as
    /// it was, naming none. Taken back, the column is gone and what stayed still stands.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_004_ASecondStepCodePendingWhenItsRecordGainsACredentialIsRemovedAndEveryOtherStaysAsync()
    {
        string moved = await database.CreateDatabaseAsync("signin_link_credentials");
        string naming;
        string before;

        await using (StoreContext migrating = DatabaseFixture.Context(moved))
        {
            string[] declared = [.. migrating.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal)];

            naming = declared.Single(migration =>
                migration.EndsWith("_" + nameof(NameTheCredentialASecondStepCodeIsIssuedFor), StringComparison.Ordinal));
            before = declared[Array.IndexOf(declared, naming) - 1];

            await migrating.GetService<IMigrator>().MigrateAsync(before, TestContext.Current.CancellationToken);
        }

        await using var connection = new NpgsqlConnection(moved);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var values = new
        {
            subject = Subjects.New().Value,
            texted = new byte[32],
            linked = Enumerable.Repeat((byte)1, 32).ToArray(),
            mailed = Enumerable.Repeat((byte)2, 32).ToArray(),
            code = new byte[] { 1 },
            at = Noon,
            expiry = Noon.AddMinutes(10),
        };

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.accounts (subject, state, created_at)
            VALUES (@subject, 'active', @at);
            INSERT INTO identity.signin_links
                (token, subject, factor, enc_code, issued_at, expires_at, wrong_attempts)
            VALUES
                (@texted, @subject, 'phoneCode', @code, @at, @expiry, 0),
                (@linked, @subject, 'phoneLink', @code, @at, @expiry, 0),
                (@mailed, @subject, 'emailCode', @code, @at, @expiry, 2);
            """,
            values);

        await using (StoreContext migrating = DatabaseFixture.Context(moved))
        {
            await migrating.GetService<IMigrator>().MigrateAsync(naming, TestContext.Current.CancellationToken);
        }

        (string Factor, Guid? Credential, int WrongAttempts)[] carried =
        [
            .. await connection.QueryAsync<(string, Guid?, int)>(
                "SELECT factor, credential, wrong_attempts FROM identity.signin_links ORDER BY factor"),
        ];

        await using (StoreContext migrating = DatabaseFixture.Context(moved))
        {
            await migrating.GetService<IMigrator>().MigrateAsync(before, TestContext.Current.CancellationToken);
        }

        Assert.Equal([("emailCode", null, 2), ("phoneLink", null, 0)], carried);
        Assert.Equal(
            ["emailCode", "phoneLink"],
            await connection.QueryAsync<string>("SELECT factor FROM identity.signin_links ORDER BY factor"));
        Assert.Equal(
            0,
            await connection.ExecuteScalarAsync<int>(
                """
                SELECT count(*) FROM information_schema.columns
                WHERE table_schema = 'identity' AND table_name = 'signin_links' AND column_name = 'credential'
                """));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private PendingSignInStore Store(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness, new DataConnections(context));
}
