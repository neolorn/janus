using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Registration;

/// <summary>
/// A registration a social provider supplies the address for: which addresses the
/// sign-in itself verifies, which are sent one code, and what an identity already
/// linked or an address already held makes of the attempt (REG-IDENT-008,
/// IDN-ACCT-001).
/// </summary>
public sealed partial class RegistrationServiceTests
{
    private const string Gmail = "person@gmail.com";
    private const string Relay = "x7k2mq9p4r@privaterelay.appleid.com";
    private const string Workspace = "person@company.test";
    private const string GoogleSubject = "110169484474386276334";
    private const string AppleSubject = "001234.5a6b7c8d9e0f.1234";

    /// <summary>
    /// REG-IDENT-008 AC1: Continue with Google on a gmail.com address reaches the
    /// confirm step with the address verified and locked, and no code is sent.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_008_AC1_AGmailAddressIsVerifiedByTheSignInAndReachesConfirmAsync()
    {
        _configuration.Set(Settings.RegistrationPhone, AttributeRequirement.Optional);

        RegistrationSessionId session = await AgedAsync();

        ProvidedRegistration provided = Ok(await ProvidedAsync(
            session,
            Factor.Google,
            GoogleSubject,
            Gmail,
            verified: true));

        Assert.False(provided.Linked);
        Assert.True(Identity(session, IdentifierKind.Email).IsVerified);
        Assert.True(Identity(session, IdentifierKind.Email).IsLocked);
        Assert.Empty(_notifications.Sent);

        _ = Ok(await Service.SkipPhoneAsync(session, TestContext.Current.CancellationToken));

        RegistrationState confirmed = Ok(await Service.ConfirmAsync(
            session,
            TestContext.Current.CancellationToken));

        Assert.Equal(RegistrationStep.Terms, confirmed.Step);
        Assert.Empty(_notifications.Sent);
    }

    /// <summary>
    /// REG-IDENT-008: Google operates the mailboxes of the Workspace domain its token
    /// asserts, and of no other domain the token does not assert.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_008_AWorkspaceDomainTheTokenAssertsIsVerifiedByTheSignInAsync()
    {
        RegistrationSessionId asserted = await AgedAsync();
        RegistrationSessionId unasserted = await AgedAsync();

        _ = Ok(await ProvidedAsync(asserted, Factor.Google, GoogleSubject, Workspace, verified: true, "company.test"));
        _ = Ok(await ProvidedAsync(unasserted, Factor.Google, "another-subject", Workspace, verified: true, "other.test"));

        Assert.True(Identity(asserted, IdentifierKind.Email).IsVerified);
        Assert.False(Identity(unasserted, IdentifierKind.Email).IsVerified);
        Assert.NotNull(Identity(unasserted, IdentifierKind.Email).Code);
    }

    /// <summary>
    /// REG-IDENT-008: an Apple relay address is verified by the sign-in, as the
    /// provider-domain list of D-146 names it, and is locked.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_008_AnAppleRelayAddressIsVerifiedByTheSignInAsync()
    {
        RegistrationSessionId session = await AgedAsync();

        _ = Ok(await ProvidedAsync(session, Factor.Apple, AppleSubject, Relay, verified: true));

        Assert.True(Identity(session, IdentifierKind.Email).IsVerified);
        Assert.True(Identity(session, IdentifierKind.Email).IsLocked);
        Assert.Empty(_notifications.Sent);
    }

    /// <summary>
    /// REG-IDENT-008 AC2: Continue with Apple on a third-party address sends one code,
    /// and the address is verified by that code and by nothing else.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_008_AC2_AThirdPartyAddressFromAppleIsSentOneCodeAsync()
    {
        RegistrationSessionId session = await AgedAsync();

        _ = Ok(await ProvidedAsync(session, Factor.Apple, AppleSubject, Address, verified: true));

        Assert.False(Identity(session, IdentifierKind.Email).IsVerified);
        Assert.Equal(
            MessageKind.VerificationCode,
            Assert.Single(_notifications.Mail).Message);

        await VerifiedAsync(session, IdentifierKind.Email);

        Assert.True(Identity(session, IdentifierKind.Email).IsVerified);
    }

    /// <summary>
    /// REG-IDENT-008: a mailbox the provider operates counts as verified only where
    /// the provider says it verified the address.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_008_AnAddressTheProviderDoesNotCallVerifiedIsSentACodeAsync()
    {
        RegistrationSessionId session = await AgedAsync();

        _ = Ok(await ProvidedAsync(session, Factor.Google, GoogleSubject, Gmail, verified: false));

        Assert.False(Identity(session, IdentifierKind.Email).IsVerified);
        _ = Assert.Single(_notifications.Mail);
    }

    /// <summary>
    /// REG-IDENT-008 AC3: an identity already linked makes the attempt a sign-in, and
    /// the registration takes nothing from it.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_008_AC3_ALinkedIdentityMakesTheAttemptASignInAsync()
    {
        await _authenticators.LinkAsync(
            Authenticator.Linked(
                AuthenticatorId.New(_clock),
                SubjectId.New(_randomness),
                Factor.Google,
                CredentialLabel.Of(Browser),
                Noon),
            GoogleSubject,
            TestContext.Current.CancellationToken);

        RegistrationSessionId session = await AgedAsync();

        ProvidedRegistration provided = Ok(await ProvidedAsync(
            session,
            Factor.Google,
            GoogleSubject,
            Gmail,
            verified: true));

        Assert.True(provided.Linked);
        Assert.Null(provided.State);
        Assert.Empty(Live(session).Identifiers);
        Assert.Empty(Live(session).Credentials);
        Assert.Equal(RegistrationStep.Email, Live(session).Step);
    }

    /// <summary>
    /// REG-IDENT-008 AC4: an address another account holds answers as a fresh one does,
    /// field for field, sends no code, and tells the holder once in the window.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_008_AC4_AnAddressAnotherAccountHoldsAnswersAsAFreshOneAsync()
    {
        RegistrationSessionId fresh = await AgedAsync();
        RegistrationSessionId duplicate = await AgedAsync();
        RegistrationSessionId again = await AgedAsync();

        RegistrationState first = Ok(await ProvidedAsync(fresh, Factor.Apple, AppleSubject, Address, verified: true)).State!;

        _directory.Held(IdentifierKind.Email, Address, SubjectId.New(_randomness));

        Later();

        RegistrationState second = Ok(await ProvidedAsync(duplicate, Factor.Apple, "another-subject", Address, verified: true)).State!;

        Later();

        _ = Ok(await ProvidedAsync(again, Factor.Apple, "a-third-subject", Address, verified: true));

        Assert.Equal(first.Step, second.Step);
        Assert.Equal(first.ExpiresAt, second.ExpiresAt);
        Assert.Equal(first.Security.Password, second.Security.Password);
        Assert.Equal(first.Security.SecondStep, second.Security.SecondStep);
        Assert.Equal(first.Security.RecoveryCodes, second.Security.RecoveryCodes);
        Assert.Equal(
            Assert.Single(first.Identifiers) with { Id = Assert.Single(second.Identifiers).Id },
            Assert.Single(second.Identifiers));
        Assert.Null(Identity(duplicate, IdentifierKind.Email).Code);
        Assert.Null(Identity(again, IdentifierKind.Email).Code);
        Assert.Equal(1, _notifications.Mail.Count(sent => sent.Message is MessageKind.AccountExists));
    }

    /// <summary>
    /// REG-IDENT-008 AC4 and IDN-ACCT-001: a mailbox the provider operates is not
    /// vouched for where another account holds the address; the address is never a
    /// key, so the attempt goes on as any duplicate does.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_008_AC4_AProviderOperatedAddressAnotherAccountHoldsIsNotVouchedForAsync()
    {
        _directory.Held(IdentifierKind.Email, Gmail, SubjectId.New(_randomness));

        RegistrationSessionId session = await AgedAsync();

        _ = Ok(await ProvidedAsync(session, Factor.Google, GoogleSubject, Gmail, verified: true));

        Assert.False(Identity(session, IdentifierKind.Email).IsVerified);
        Assert.Null(Identity(session, IdentifierKind.Email).Code);
        Assert.Equal(MessageKind.AccountExists, Assert.Single(_notifications.Mail).Message);
    }

    /// <summary>
    /// IDN-ACCT-001 AC2: an account a provider's sign-in registered is a record of its
    /// own, with its own identifier and its own subject, which the provider's identity
    /// is only a credential of.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_001_AC2_AnAccountRegisteredThroughAProviderHasItsOwnRecordAsync()
    {
        _configuration.Set(Settings.RegistrationPhone, AttributeRequirement.Optional);

        RegistrationSessionId session = await AgedAsync();

        _ = Ok(await ProvidedAsync(session, Factor.Google, GoogleSubject, Gmail, verified: true));
        _ = Ok(await Service.SkipPhoneAsync(session, TestContext.Current.CancellationToken));
        _ = Ok(await Service.ConfirmAsync(session, TestContext.Current.CancellationToken));

        RegistrationCompleted completed = Ok(await AcceptedAsync(session));

        NewAccount created = Assert.Single(_directory.Created);
        Authenticator linked = (await _authenticators.ByProviderAsync(
            Factor.Google,
            GoogleSubject,
            TestContext.Current.CancellationToken))!;

        Assert.Equal(completed.Subject, created.Subject);
        Assert.Equal(Gmail, Assert.Single(created.Identifiers).Canonical);
        Assert.Equal(created.Subject, linked.Subject);
        Assert.DoesNotContain(GoogleSubject, created.Subject.ToString(), System.StringComparison.Ordinal);
    }

    private async Task<Result<ProvidedRegistration>> ProvidedAsync(
        RegistrationSessionId session,
        Factor provider,
        string subject,
        string email,
        bool verified,
        string? hostedDomain = null) =>
        await Service.ProvidedAsync(
            session,
            provider,
            subject,
            ProvidedAddress.Of(provider, email, verified, hostedDomain),
            CredentialLabel.Of(Browser),
            TestContext.Current.CancellationToken);
}
