using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Core.Configuration;
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

    /// <summary>
    /// AUTH-FACT-008 AC4, REG-SESS-006: the instant the security step returned the
    /// recovery codes is staged with them in the session's document and comes back as
    /// it was staged.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC4_TheInstantTheCodesWereReturnedIsStagedWithThemAsync()
    {
        RegistrationSession opened = Opened();
        var code = PasswordHash.Of(new Argon2StrengthClass(19456, 2), 1, new byte[16], new byte[32]);
        opened.StageRecoveryCodes([code], Noon.AddMinutes(3));

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(opened, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using StoreContext reading = database.Context();
        RegistrationSession read = Assert.IsType<RegistrationSession>(
            await Store(reading).FindAsync(opened.Id, TestContext.Current.CancellationToken));

        Assert.Equal(Noon.AddMinutes(3), read.RecoveryCodesViewedAt);
        Assert.Equal(code.Encoded, Assert.Single(read.RecoveryCodes!).Encoded);
    }

    /// <summary>
    /// AUTH-FACT-014, REG-SESS-006: a key staged on the session comes back as it was
    /// staged, its public key and its transports as the creation gave them.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AStagedKeyKeepsItsPublicKeyAndItsTransportsAsync()
    {
        RegistrationSession opened = Opened();
        var staged = new StagedCredential(
            AuthenticatorId.New(TimeProvider.System),
            Factor.Passkey,
            Label("This phone"),
            Totp: null,
            new WebAuthnMaterial(
                new byte[] { 1, 2, 3 },
                new byte[] { 4, 5, 6 },
                Algorithm: -8,
                "example.test",
                Counter: null,
                BackupEligible: true,
                BackupState: false,
                Transports: ["internal", "hybrid"]));
        opened.Enrol(staged);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(opened, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        RegistrationSession read = Assert.IsType<RegistrationSession>(
            await Store(reading).FindAsync(opened.Id, TestContext.Current.CancellationToken));
        WebAuthnMaterial held = Assert.Single(read.Credentials).WebAuthn!;

        Assert.Equal(new byte[] { 1, 2, 3 }, held.CredentialId.ToArray());
        Assert.Equal(new byte[] { 4, 5, 6 }, held.PublicKey.ToArray());
        Assert.Equal((-8, "example.test", true, false), (held.Algorithm, held.RelyingPartyId, held.BackupEligible, held.BackupState));
        Assert.Null(held.Counter);
        Assert.Equal(["internal", "hybrid"], held.Transports);
    }

    /// <summary>
    /// REG-SESS-001 (D-224 item 1): a session whose every optional array is null reads
    /// back with each still null, and stages no link row.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_SESS_001_ASessionWithEveryOptionalArrayNullReadsBackWithEachNullAsync()
    {
        RegistrationSession opened = Opened();
        var staged = IdentifierId.New(TimeProvider.System);
        opened.Stage(StagedIdentity.Of(staged, IdentifierKind.Email, "person@example.test", "person@example.test"));
        opened.Enrol(new StagedCredential(
            AuthenticatorId.New(TimeProvider.System),
            Factor.Google,
            Label("Google"),
            Totp: null,
            WebAuthn: null,
            ProviderSubject: "provider-subject"));

        await AddAsync(opened);

        await using StoreContext reading = database.Context();
        RegistrationSession read = Assert.IsType<RegistrationSession>(
            await Store(reading).FindAsync(opened.Id, TestContext.Current.CancellationToken));
        StagedCredential credential = Assert.Single(read.Credentials);

        Assert.Null(read.Identity(staged)!.Link);
        Assert.Null(credential.Totp);
        Assert.Null(credential.WebAuthn);
        Assert.Equal("provider-subject", credential.ProviderSubject);
        Assert.Null(read.RecoveryCodes);
        Assert.Null(read.Ceremony);
        Assert.Null(read.Generator);
        Assert.Equal(0, await LinkRowsAsync(opened.Id));
    }

    /// <summary>
    /// REG-SESS-001 (D-224 item 1): a session whose every array is set reads back with
    /// each as it was written, a staged generator and its TOTP credential whole, and the
    /// identifier's link found by its fingerprint.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_SESS_001_ASessionWithEveryArraySetReadsBackAsItWasWrittenAsync()
    {
        RegistrationSession opened = Opened();
        var staged = IdentifierId.New(TimeProvider.System);
        byte[] link = SHA256.HashData(Encoding.UTF8.GetBytes("a link token"));
        var code = PasswordHash.Of(new Argon2StrengthClass(19456, 2), 1, new byte[16], new byte[32]);
        var identity = StagedIdentity.Of(staged, IdentifierKind.Email, "person@example.test", "person@example.test");
        identity.Linked(link);
        opened.Stage(identity);
        opened.Enrol(new StagedCredential(
            AuthenticatorId.New(TimeProvider.System),
            Factor.Totp,
            Label("Authenticator"),
            new TotpMaterial(new byte[] { 7, 8, 9, 10 }, ConsumedStep: 41),
            WebAuthn: null));
        opened.Enrol(new StagedCredential(
            AuthenticatorId.New(TimeProvider.System),
            Factor.SecurityKey,
            Label("Key"),
            Totp: null,
            new WebAuthnMaterial(
                new byte[] { 1, 2, 3 },
                new byte[] { 4, 5, 6 },
                Algorithm: -7,
                "example.test",
                Counter: 3,
                BackupEligible: false,
                BackupState: false,
                Transports: ["usb"])));
        opened.Begin(new StagedGenerator(AuthenticatorId.New(TimeProvider.System), Label("Second"), new byte[] { 11, 12 }));
        opened.StageRecoveryCodes([code], Noon);

        await AddAsync(opened);

        await using StoreContext reading = database.Context();
        RegistrationSession read = Assert.IsType<RegistrationSession>(
            await Store(reading).FindByLinkAsync(link, TestContext.Current.CancellationToken));
        TotpMaterial totp = read.Credentials.Single(credential => credential.Factor is Factor.Totp).Totp!;
        WebAuthnMaterial key = read.Credentials.Single(credential => credential.Factor is Factor.SecurityKey).WebAuthn!;

        Assert.Equal(opened.Id, read.Id);
        Assert.Equal(link, read.Identity(staged)!.Link);
        Assert.Equal(new byte[] { 7, 8, 9, 10 }, totp.Secret.ToArray());
        Assert.Equal(41, totp.ConsumedStep);
        Assert.Equal(new byte[] { 1, 2, 3 }, key.CredentialId.ToArray());
        Assert.Equal(new byte[] { 4, 5, 6 }, key.PublicKey.ToArray());
        Assert.Equal(new byte[] { 11, 12 }, read.Generator!.Secret.ToArray());
        Assert.Equal(code.Encoded, Assert.Single(read.RecoveryCodes!).Encoded);
        Assert.Equal(1, await LinkRowsAsync(opened.Id));
    }

    /// <summary>
    /// REG-SESS-001 (D-224 item 1): an empty array reads back empty, not null, and a
    /// link with an empty fingerprint is never written as a link row.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_SESS_001_AnEmptyArrayReadsBackEmptyAndWritesNoLinkRowAsync()
    {
        RegistrationSession opened = Opened();
        var staged = IdentifierId.New(TimeProvider.System);
        var identity = StagedIdentity.Of(staged, IdentifierKind.Email, "person@example.test", "person@example.test");
        identity.Linked([]);
        opened.Stage(identity);
        opened.StageRecoveryCodes([], Noon);

        await AddAsync(opened);

        await using StoreContext reading = database.Context();
        RegistrationSession read = Assert.IsType<RegistrationSession>(
            await Store(reading).FindAsync(opened.Id, TestContext.Current.CancellationToken));

        Assert.Empty(Assert.IsType<byte[]>(read.Identity(staged)!.Link));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<PasswordHash>>(read.RecoveryCodes));
        Assert.Equal(0, await LinkRowsAsync(opened.Id));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private async Task AddAsync(RegistrationSession session)
    {
        await using StoreContext writing = database.Context();
        await Store(writing).AddAsync(session, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> LinkRowsAsync(RegistrationSessionId session)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM identity.registration_links WHERE session = @session",
            new { session = session.Value });
    }

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
