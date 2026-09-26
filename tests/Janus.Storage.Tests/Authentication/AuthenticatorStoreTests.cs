using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Factors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// Enrolled credentials as the <c>authenticators</c> rows carry them (AUTH-FACT-001,
/// AUTH-FACT-006, AUTH-FACT-013).
/// </summary>
[Trait("kind", "integration")]
public sealed class AuthenticatorStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] PublicKey = [4, 9, 9, 9];

    private readonly Deployment _deployment = new(database);

    // The tests share one database, so each links an identity no other test holds.
    private readonly string _providerSubject = "001234." + Guid.NewGuid().ToString("N") + ".0456";

    /// <summary>
    /// AUTH-FACT-006 AC1: the shared secret is not in the table in plain, so a dump
    /// without the key-encryption key yields no code generator.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_006_AC1_TheSecretIsNotReadableFromTheTableAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        byte[] secret = Secret();

        await WrittenAsync(Authenticator.EnrollingTotp(
            AuthenticatorId.New(TimeProvider.System),
            subject,
            Label("this phone"),
            secret,
            Noon));

        await using StoreContext reading = database.Context();
        AuthenticatorRecord stored = await reading.Authenticators
            .SingleAsync(held => held.Subject == subject, TestContext.Current.CancellationToken);

        Assert.NotNull(stored.TotpSecret);
        Assert.Equal(PersonalDataFormat.Marker, stored.TotpSecret[0]);
        Assert.Equal(-1, stored.TotpSecret.AsSpan().IndexOf(secret));
    }

    /// <summary>
    /// AUTH-FACT-006: the secret read back is the secret enrolled, so the enrolment
    /// still verifies a code after a restart.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_006_TheSecretReadsBackByteForByteAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        byte[] secret = Secret();
        var id = AuthenticatorId.New(TimeProvider.System);

        await WrittenAsync(Authenticator.EnrollingTotp(id, subject, Label("this phone"), secret, Noon));

        await using StoreContext reading = database.Context();
        Authenticator read = Assert.IsType<Authenticator>(
            await Store(reading).FindAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal(secret, read.Totp!.Secret.ToArray());
        Assert.False(read.Confirmed);
        Assert.Equal(Noon, read.AddedAt);
    }

    /// <summary>
    /// AUTH-FACT-013: a credential is resolved by what the browser returns, which is
    /// how a discoverable credential names the account rather than the other way
    /// round.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_013_ACredentialIsFoundByWhatTheBrowserReturnsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        byte[] credentialId = Secret();

        await WrittenAsync(Authenticator.WebAuthnCredential(
            AuthenticatorId.New(TimeProvider.System),
            subject,
            Factor.Passkey,
            Label("this laptop"),
            new WebAuthnMaterial(credentialId, PublicKey, -7, "example.com", 4, true, false),
            Noon));

        await using StoreContext reading = database.Context();
        Authenticator read = Assert.IsType<Authenticator>(
            await Store(reading).ByCredentialAsync(credentialId, TestContext.Current.CancellationToken));

        Assert.Equal(Factor.Passkey, read.Factor);
        Assert.Equal("example.com", read.WebAuthn!.RelyingPartyId);
        Assert.Equal(4u, read.WebAuthn.Counter!.Value);
        Assert.True(read.WebAuthn.BackupEligible);
        Assert.False(read.WebAuthn.BackupState);
        Assert.Null(read.Totp);
    }

    /// <summary>
    /// AUTH-FACT-001 AC5: a label the account already holds for that kind is refused
    /// by the database and not by a read before a write.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_001_AC5_ALabelHeldTwiceForOneKindIsRefusedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await WrittenAsync(Authenticator.EnrollingTotp(
            AuthenticatorId.New(TimeProvider.System),
            subject,
            Label("this phone"),
            Secret(),
            Noon));

        await Assert.ThrowsAsync<DbUpdateException>(async () => await WrittenAsync(
            Authenticator.EnrollingTotp(
                AuthenticatorId.New(TimeProvider.System),
                subject,
                Label("this phone"),
                Secret(),
                Noon)));
    }

    /// <summary>
    /// AUTH-FACT-001 AC4: the instant of last use advances on the row, and the state
    /// a loss report put the credential in is carried with it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_001_AC4_TheRowCarriesWhatTheCredentialDidAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var id = AuthenticatorId.New(TimeProvider.System);

        await WrittenAsync(Authenticator.EnrollingTotp(id, subject, Label("this phone"), Secret(), Noon));

        await using (StoreContext changing = database.Context())
        {
            Authenticator held = Assert.IsType<Authenticator>(
                await Store(changing).FindAsync(id, TestContext.Current.CancellationToken));

            held.Confirm(Noon + TimeSpan.FromMinutes(1));
            held.Consumed(57);
            held.Suspend(Noon + TimeSpan.FromDays(7));

            await Store(changing).RecordAsync(held, TestContext.Current.CancellationToken);
            await changing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        Authenticator read = Assert.IsType<Authenticator>(
            await Store(reading).FindAsync(id, TestContext.Current.CancellationToken));

        Assert.True(read.Confirmed);
        Assert.Equal(Noon + TimeSpan.FromMinutes(1), read.LastUsedAt);
        Assert.Equal(57, read.Totp!.ConsumedStep);
        Assert.Equal(AuthenticatorState.Suspended, read.State);
        Assert.Equal(Noon + TimeSpan.FromDays(7), read.InvalidatesAt);
    }

    /// <summary>
    /// An account's credentials are every credential it holds, whatever state they
    /// stand in, which is what the reachable-assurance rule reads.
    /// </summary>
    [Fact]
    public async Task OfAsync_AnAccountHoldingTwo_ReadsBothAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await WrittenAsync(Authenticator.EnrollingTotp(
            AuthenticatorId.New(TimeProvider.System),
            subject,
            Label("this phone"),
            Secret(),
            Noon));
        await WrittenAsync(Authenticator.WebAuthnCredential(
            AuthenticatorId.New(TimeProvider.System),
            subject,
            Factor.Passkey,
            Label("this laptop"),
            new WebAuthnMaterial(Secret(), PublicKey, -7, "example.com", null, false, false),
            Noon + TimeSpan.FromMinutes(1)));

        await using StoreContext reading = database.Context();
        IReadOnlyList<Authenticator> held = await Store(reading)
            .OfAsync(subject, TestContext.Current.CancellationToken);

        Assert.Equal([Factor.Totp, Factor.Passkey], [.. Kinds(held)]);
    }

    /// <summary>
    /// IDN-LIFE-012a, REG-IDENT-008: a linked identity is found by the subject the
    /// provider names it by, and only for that provider; the row holds the subject's
    /// keyed fingerprint and never the subject itself (PRIV-RIGHT-005c).
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_012a_ALinkedIdentityIsFoundByTheProvidersSubjectAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var id = AuthenticatorId.New(TimeProvider.System);

        await LinkedAsync(Authenticator.Linked(id, subject, Factor.Google, Label("Google"), Noon), _providerSubject);

        await using StoreContext reading = database.Context();
        Authenticator found = Assert.IsType<Authenticator>(
            await Store(reading).ByProviderAsync(Factor.Google, _providerSubject, TestContext.Current.CancellationToken));
        AuthenticatorRecord stored = await reading.Authenticators
            .SingleAsync(held => held.Subject == subject, TestContext.Current.CancellationToken);

        Assert.Equal(id, found.Id);
        Assert.Equal(Factor.Google, found.Factor);
        Assert.True(found.IsUsable);
        Assert.Null(await Store(reading).ByProviderAsync(Factor.Apple, _providerSubject, TestContext.Current.CancellationToken));
        Assert.Null(await Store(reading).ByProviderAsync(Factor.Google, "another", TestContext.Current.CancellationToken));
        Assert.Equal(
            Fingerprint.Compute(Encoding.UTF8.GetBytes(_providerSubject), Deployment.FingerprintKey),
            stored.ProviderSubject);
    }

    /// <summary>
    /// REG-IDENT-008: a provider's subject is linked to one account, which the
    /// database holds.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_012a_AProvidersSubjectIsLinkedOnceAsync()
    {
        SubjectId first = await _deployment.AccountAsync(Noon);
        SubjectId second = await _deployment.AccountAsync(Noon);

        await LinkedAsync(
            Authenticator.Linked(AuthenticatorId.New(TimeProvider.System), first, Factor.Apple, Label("Apple"), Noon),
            _providerSubject);

        _ = await Assert.ThrowsAsync<DbUpdateException>(() => LinkedAsync(
            Authenticator.Linked(AuthenticatorId.New(TimeProvider.System), second, Factor.Apple, Label("Apple"), Noon),
            _providerSubject));
    }

    /// <summary>
    /// IDN-LIFE-012a: a social provider's credential is not held without the subject
    /// it is found by, and nothing else holds one.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_012a_OnlyALinkedIdentityHoldsAProvidersSubjectAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        _ = await Assert.ThrowsAsync<DbUpdateException>(() => WrittenAsync(Authenticator.Linked(
            AuthenticatorId.New(TimeProvider.System),
            subject,
            Factor.Google,
            Label("Google"),
            Noon)));
        _ = await Assert.ThrowsAsync<DbUpdateException>(() => LinkedAsync(
            Authenticator.EnrollingTotp(AuthenticatorId.New(TimeProvider.System), subject, Label("this phone"), Secret(), Noon),
            _providerSubject));
    }

    /// <summary>
    /// IDN-LIFE-012a: a credential a provider's event holds reads back held, with no
    /// instant at which it is invalidated.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_012a_AHeldCredentialReadsBackHeldAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var id = AuthenticatorId.New(TimeProvider.System);

        await LinkedAsync(Authenticator.Linked(id, subject, Factor.Google, Label("Google"), Noon), _providerSubject);

        await using (StoreContext changing = database.Context())
        {
            Authenticator held = Assert.IsType<Authenticator>(
                await Store(changing).FindAsync(id, TestContext.Current.CancellationToken));

            held.Hold();

            await Store(changing).RecordAsync(held, TestContext.Current.CancellationToken);
            await changing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        Authenticator read = Assert.IsType<Authenticator>(
            await Store(reading).FindAsync(id, TestContext.Current.CancellationToken));

        Assert.True(read.IsHeldByProvider);
        Assert.Null(read.InvalidatesAt);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static IEnumerable<Factor> Kinds(IReadOnlyList<Authenticator> held)
    {
        foreach (Authenticator credential in held)
        {
            yield return credential.Factor;
        }
    }

    private static CredentialLabel Label(string entered) =>
        CredentialLabel.TryParse(entered, out CredentialLabel label)
            ? label
            : throw new Xunit.Sdk.XunitException("The label is one the chapter admits.");

    private byte[] Secret()
    {
        byte[] bytes = new byte[20];
        _deployment.Randomness.GetBytes(bytes);

        return bytes;
    }

    private async Task WrittenAsync(Authenticator credential)
    {
        await using StoreContext writing = database.Context();

        await Store(writing).AddAsync(credential, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private AuthenticatorStore Store(StoreContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness, Deployment.FingerprintKeys);

    private async Task LinkedAsync(Authenticator credential, string providerSubject)
    {
        await using StoreContext writing = database.Context();

        await Store(writing).LinkAsync(credential, providerSubject, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
