using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Passwords;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Audit;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using Xunit;
using Catalogue = Janus.Core.Configuration.Settings;

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
    /// AUTH-FACT-001 AC5, OPS-DB-001: the same label in other capitals is the same
    /// label, and the database refuses it too.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_001_AC5_ALabelHeldInOtherCapitalsIsRefusedByTheDatabaseAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await WrittenAsync(Authenticator.EnrollingTotp(
            AuthenticatorId.New(TimeProvider.System),
            subject,
            Label("this phone"),
            Secret(),
            Noon));

        _ = await Assert.ThrowsAsync<DbUpdateException>(async () => await WrittenAsync(
            Authenticator.EnrollingTotp(
                AuthenticatorId.New(TimeProvider.System),
                subject,
                Label("This PHONE"),
                Secret(),
                Noon)));
    }

    /// <summary>
    /// AUTH-FACT-001 AC5: a label is found held in other capitals, for the kind it is
    /// held for only, and not by the credential that holds it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_001_AC5_ALabelIsFoundHeldWithoutRegardToCaseAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var id = AuthenticatorId.New(TimeProvider.System);

        await WrittenAsync(Authenticator.EnrollingTotp(id, subject, Label("this phone"), Secret(), Noon));

        await using StoreContext reading = database.Context();
        AuthenticatorStore store = Store(reading);

        Assert.True(await store.LabelHeldAsync(
            subject, Factor.Totp, Label("THIS Phone"), except: null, TestContext.Current.CancellationToken));
        Assert.False(await store.LabelHeldAsync(
            subject, Factor.Passkey, Label("THIS Phone"), except: null, TestContext.Current.CancellationToken));
        Assert.False(await store.LabelHeldAsync(
            subject, Factor.Totp, Label("THIS Phone"), except: id, TestContext.Current.CancellationToken));
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
    /// AUTH-FACT-005 AC3, CONV-DESIGN-003 AC6: the same valid code presented twice at once
    /// is accepted once and refused as replayed once, the second presentation waiting for
    /// the first on the credential's row.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_005_AC3_TheSameCodeTwiceAtOnceSucceedsOnceAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var id = AuthenticatorId.New(TimeProvider.System);
        byte[] secret = Secret();
        var generator = Authenticator.EnrollingTotp(id, subject, Label("this phone"), secret, Noon);

        generator.Confirm(Noon);
        await WrittenAsync(generator);

        string code = new Totp(secret, TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(DateTime.UtcNow);

        ErrorCode?[] answers = await Task.WhenAll(PresentedAsync(subject, code), PresentedAsync(subject, code));

        Assert.Equal(1, answers.Count(answer => answer is null));
        Assert.Equal(1, answers.Count(answer => answer == ErrorCodes.CodeReplayed));
    }

    /// <summary>
    /// AUTH-FACT-014 AC3, CONV-DESIGN-003 AC6: two assertions reporting one counter at once
    /// are judged one after the other on the credential's row, so one is accepted and the
    /// other refused as a counter standing still.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_014_AC3_TwoAssertionsOfOneCounterAtOnceSucceedOnceAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        byte[] credentialId = Secret();

        await RelyingPartyAsync();
        await WrittenAsync(Authenticator.WebAuthnCredential(
            AuthenticatorId.New(TimeProvider.System),
            subject,
            Factor.Passkey,
            Label("this laptop"),
            new WebAuthnMaterial(credentialId, PublicKey, -7, "example.com", 4, true, false),
            Noon));

        var assertion = new WebAuthnAssertion(credentialId, "example.com", UserVerified: true, Counter: 5);

        ErrorCode?[] answers = await Task.WhenAll(AssertedAsync(assertion), AssertedAsync(assertion));

        Assert.Equal(1, answers.Count(answer => answer is null));
        Assert.Equal(1, answers.Count(answer => answer == ErrorCodes.WebAuthnCounterMismatch));
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

    /// <summary>
    /// IDN-LIFE-012 AC3, CONV-DESIGN-003 AC6: two unlinks at once, each of an identity
    /// the other leaves the account to sign in with, read the account's credentials
    /// under their locks, so the second finds the first gone and the last way in stays.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_012_AC3_TwoUnlinksAtOnceLeaveAWayInAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var google = Authenticator.Linked(AuthenticatorId.New(TimeProvider.System), subject, Factor.Google, Label("Google"), Noon);
        var apple = Authenticator.Linked(AuthenticatorId.New(TimeProvider.System), subject, Factor.Apple, Label("Apple"), Noon);

        await LinkedAsync(google, _providerSubject);
        await LinkedAsync(apple, "001234." + Guid.NewGuid().ToString("N") + ".0789");

        bool[] unlinked = await Task.WhenAll(UnlinkedAsync(subject, google.Id), UnlinkedAsync(subject, apple.Id));

        await using StoreContext reading = database.Context();

        Assert.Equal(1, unlinked.Count(answer => answer));
        Assert.Single(await Store(reading).OfAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-LIFE-012, CONV-DESIGN-003 AC6: two links of one provider to one account at
    /// once, each of a different identity, are judged under the lock on the account's
    /// row, so the second finds the first and the account holds one identity of it.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_012_TwoLinksOfOneProviderAtOnceLinkOnceAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        bool[] linked = await Task.WhenAll(
            LinkedOnceAsync(subject, "001234." + Guid.NewGuid().ToString("N") + ".0001"),
            LinkedOnceAsync(subject, "001234." + Guid.NewGuid().ToString("N") + ".0002"));

        await using StoreContext reading = database.Context();

        Assert.Equal(1, linked.Count(answer => answer));
        Assert.Single(await Store(reading).OfAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    // Each link is its own request, judging the provider's identity on the account under
    // the account's lock as the credential service does.
    private async Task<bool> LinkedOnceAsync(SubjectId subject, string providerSubject)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        AuthenticatorStore store = Store(context);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        _ = await new AccountStore(context).HoldAsync(subject, TestContext.Current.CancellationToken);

        bool free = (await store.OfAsync(subject, TestContext.Current.CancellationToken))
            .All(credential => credential.Factor != Factor.Google);

        if (free)
        {
            await store.LinkAsync(
                Authenticator.Linked(AuthenticatorId.New(TimeProvider.System), subject, Factor.Google, Label("Google"), Noon),
                providerSubject,
                TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return free;
    }

    // Each unlink is its own request, keeping another way in as the credential service
    // does: the identity goes only where the set read under the locks holds another.
    private async Task<bool> UnlinkedAsync(SubjectId subject, AuthenticatorId going)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        AuthenticatorStore store = Store(context);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        IReadOnlyList<Authenticator> standing = await store.OfForUpdateAsync(subject, TestContext.Current.CancellationToken);
        bool kept = standing.Any(credential => credential.Id == going) && standing.Count > 1;

        if (kept)
        {
            await store.RemoveAsync(going, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return kept;
    }

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

    // Each presentation is its own request: its own context, connection and transaction.
    private async Task<ErrorCode?> PresentedAsync(SubjectId subject, string code)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        var codes = new TotpService(
            Store(context),
            new PasswordStore(context),
            new ConfigurationStore(context, new DataConnections(context)),
            work,
            TimeProvider.System,
            _deployment.Randomness);

        return (await codes.PresentAsync(subject, code, TestContext.Current.CancellationToken))
            .Match<ErrorCode?>(_ => null, error => error.Code);
    }

    // Each assertion is its own request: its own context, connection and transaction.
    private async Task<ErrorCode?> AssertedAsync(WebAuthnAssertion assertion)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        var connections = new DataConnections(context);

        var keys = new WebAuthnService(
            Store(context),
            new PasswordStore(context),
            new CredentialAudit(
                new AuditStore(context, connections, _deployment.Ring, _deployment.Randomness),
                TimeProvider.System),
            new ConfigurationStore(context, connections),
            work,
            TimeProvider.System,
            _deployment.Randomness);

        return (await keys.PresentAsync(assertion, identified: true, TestContext.Current.CancellationToken))
            .Match<ErrorCode?>(_ => null, error => error.Code);
    }

    // The relying party a ceremony is judged against, which the deployment names.
    private async Task RelyingPartyAsync()
    {
        await using StoreContext context = database.Context();

        context.Settings.Add(new SettingRecord
        {
            Key = Catalogue.WebAuthnRelyingPartyId.Key,
            Value = Catalogue.WebAuthnRelyingPartyId.Write("example.com"),
        });
        context.Settings.Add(new SettingRecord
        {
            Key = Catalogue.WebAuthnOrigins.Key,
            Value = Catalogue.WebAuthnOrigins.Write(["https://example.com"]),
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private AuthenticatorStore Store(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);

    private async Task LinkedAsync(Authenticator credential, string providerSubject)
    {
        await using StoreContext writing = database.Context();

        await Store(writing).LinkAsync(credential, providerSubject, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
