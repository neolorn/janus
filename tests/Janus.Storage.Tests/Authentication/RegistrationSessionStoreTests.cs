using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Storage.Authentication.Registration;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What a registration session stages, as the database keeps it (REG-SESS-001).
/// </summary>
/// <remarks>The port implementations are tested against the real database (D-156).</remarks>
[Trait("kind", "integration")]
public sealed class RegistrationSessionStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// IDN-LIFE-009a and REG-INV-001: a registration an invitation opened keeps the
    /// invitation with everything else it staged, and one opened without keeps none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_009a_ARegistrationKeepsTheInvitationThatOpenedItAsync()
    {
        RegistrationSession invited = Opened();
        RegistrationSession open = Opened();
        var invitation = InvitationId.New(TimeProvider.System);

        invited.Invited(invitation);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(invited, TestContext.Current.CancellationToken);
            await Store(writing).AddAsync(open, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            invitation,
            (await Store(reading).FindAsync(invited.Id, TestContext.Current.CancellationToken))!.Invitation);
        Assert.Null((await Store(reading).FindAsync(open.Id, TestContext.Current.CancellationToken))!.Invitation);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC18: what a session stages belongs to no subject yet and is bound
    /// to the session's own row, so its value and wrapped key moved onto another session's
    /// row do not open there.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC18_StagedValuesDoNotOpenOnAnotherRowAsync()
    {
        RegistrationSession moved = Opened();
        RegistrationSession other = Opened();

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(moved, TestContext.Current.CancellationToken);
            await Store(writing).AddAsync(other, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (NpgsqlConnection connection = await database.OpenAsync())
        {
            await connection.ExecuteAsync(
                """
                UPDATE identity.registration_sessions AS other
                SET wrapped_key = moved.wrapped_key, enc_session = moved.enc_session
                FROM identity.registration_sessions AS moved
                WHERE other.id = @other AND moved.id = @moved;
                """,
                new { other = other.Id.Value, moved = moved.Id.Value });
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            moved.Id,
            (await Store(reading).FindAsync(moved.Id, TestContext.Current.CancellationToken))?.Id);
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await Store(reading).FindAsync(other.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-SESS-003, CONV-DESIGN-003 AC6: an identifier verified by several requests
    /// at once is decided each time on the session's row under its lock, so the first
    /// verifies it and every other finds it verified. The wrong tries of its code are
    /// counted on the verification-code record (AUTH-FACT-004 AC4).
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_SESS_003_AnIdentifierVerifiedAtOnceIsVerifiedOnceAsync()
    {
        RegistrationSession opened = Opened();
        var staged = IdentifierId.New(TimeProvider.System);

        opened.Stage(StagedIdentity.Of(staged, IdentifierKind.Email, "person@example.test", "person@example.test"));

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(opened, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        bool[] verified = await Task.WhenAll(
            VerifiedAsync(opened.Id, staged),
            VerifiedAsync(opened.Id, staged),
            VerifiedAsync(opened.Id, staged));

        await using StoreContext reading = database.Context();
        RegistrationSession read = Assert.IsType<RegistrationSession>(
            await Store(reading).FindAsync(opened.Id, TestContext.Current.CancellationToken));

        Assert.Single(verified, by => by);
        Assert.Equal(Noon, read.Identity(staged)!.VerifiedAt);
    }

    /// <summary>
    /// REG-SESS-001 AC5: the ceremony a session has open and the generator it has
    /// begun are kept in the session's own encrypted document, so both come back as
    /// they were staged, a dump of the row reads neither the challenge nor the
    /// generator's secret, and the row's removal leaves neither behind.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_SESS_001_AC5_AnOpenCeremonyAndABegunGeneratorAreKeptEncryptedOnTheSessionAsync()
    {
        const string challenge = "a-challenge-the-server-issued-0123456789";
        byte[] secret = RandomNumberGenerator.GetBytes(20);
        RegistrationSession opened = Opened();
        var generator = AuthenticatorId.New(TimeProvider.System);
        opened.Open(new StagedCeremony(Factor.Passkey, challenge, Noon.AddMinutes(10)));
        opened.Begin(new StagedGenerator(generator, Label("Authenticator"), secret));

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(opened, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        RegistrationSession read;
        byte[] dumped;

        await using (StoreContext reading = database.Context())
        {
            read = Assert.IsType<RegistrationSession>(
                await Store(reading).FindAsync(opened.Id, TestContext.Current.CancellationToken));
        }

        await using (NpgsqlConnection connection = await database.OpenAsync())
        {
            dumped = await connection.QuerySingleAsync<byte[]>(
                "SELECT enc_session FROM identity.registration_sessions WHERE id = @id;",
                new { id = opened.Id.Value });
        }

        await using (StoreContext removing = database.Context())
        {
            await Store(removing).RemoveAsync(opened.Id, TestContext.Current.CancellationToken);
            await removing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext after = database.Context();

        Assert.Equal(new StagedCeremony(Factor.Passkey, challenge, Noon.AddMinutes(10)), read.Ceremony);
        Assert.Equal(generator, read.Generator?.Id);
        Assert.Equal("Authenticator", read.Generator?.Label.Value);
        Assert.Equal(secret, read.Generator?.Secret.ToArray());
        Assert.Equal(-1, dumped.AsSpan().IndexOf(secret));
        Assert.Equal(-1, dumped.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Convert.ToBase64String(secret))));
        Assert.Equal(-1, dumped.AsSpan().IndexOf(Encoding.UTF8.GetBytes(challenge)));
        Assert.Null(await Store(after).FindAsync(opened.Id, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    // Each verification is its own request, deciding on the session as the
    // registration service does, and says whether it was the one that verified.
    private async Task<bool> VerifiedAsync(RegistrationSessionId session, IdentifierId staged)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        RegistrationSessionStore store = Store(context);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        RegistrationSession held = Assert.IsType<RegistrationSession>(
            await store.FindForUpdateAsync(session, TestContext.Current.CancellationToken));

        bool verifying = !held.Identity(staged)!.IsVerified;

        if (verifying)
        {
            held.Identity(staged)!.Verify(Noon);

            await store.RecordAsync(held, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return verifying;
    }

    private static RegistrationSession Opened() =>
        RegistrationSession.Open(
            RegistrationSessionId.New(TimeProvider.System),
            Subjects.New(),
            "web",
            "en",
            "198.51.100.7",
            Noon,
            TimeSpan.FromHours(24));

    private static CredentialLabel Label(string written) =>
        CredentialLabel.TryParse(written, out CredentialLabel label)
            ? label
            : throw new Xunit.Sdk.XunitException(written);

    private RegistrationSessionStore Store(StoreContext context) =>
        new(context, new DataConnections(context), _deployment.DataKey(context), _deployment.Randomness);
}
