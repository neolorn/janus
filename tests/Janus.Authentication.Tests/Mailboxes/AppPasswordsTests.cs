using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Mailboxes;

/// <summary>
/// The mail app passwords of the signed-in person: created, listed and revoked at the
/// mail server with the person's token, gated, notified and audited, and kept nowhere
/// on the library's side (REG-MAIL-002, INT-MAIL-010).
/// </summary>
[Trait("kind", "unit")]
public sealed class AppPasswordsTests : IAsyncDisposable
{
    private const string Mail = "person@staff.test";

    private const string Source = "198.51.100.7";

    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SessionOrigin Somewhere =
        new(Source, new DeviceDescription("Firefox", "Fedora"));

    private readonly AccountDirectoryInMemory _accounts = new(PreferenceDeclarations.None);
    private readonly MailboxStoreInMemory _mailboxes = new();
    private readonly MailServerInMemory _server = new();
    private readonly SessionStoreInMemory _sessions = new();
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly NotificationHandlerInMemory _notifications = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly CredentialAuditInMemory _audit = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly MailServerTokensInMemory _tokens;
    private readonly SubjectId _person;
    private readonly SessionId _session;

    /// <summary>
    /// An active account holding an enabled mailbox and a verified email, signed in a
    /// moment ago with a password.
    /// </summary>
    public AppPasswordsTests()
    {
        _tokens = new MailServerTokensInMemory(_sessions, _clock);
        _person = SubjectId.New(_randomness);
        _accounts.Stands(_person, AccountState.Active);
        _passwords.Hold(_person, Noon);
        _ = _identifiers.Verified(_person, IdentifierKind.Email, Mail);

        var mailbox = Mailbox.Reserved(Parsed(Mail), Noon.AddDays(-1));

        mailbox.Hold(_person);
        _mailboxes.Held.Add(mailbox);
        _session = Opened(_person, Noon);
    }

    private AppPasswords Passwords => Built(_server);

    private AccessContext Asking => AccessContext.Of(_person);

    private static DateTimeOffset Stale =>
        Noon - Settings.SessionStepUpRecency.Default - TimeSpan.FromMinutes(1);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// REG-MAIL-002 AC1, INT-MAIL-010 AC1 and AC4: creation is one call to the server
    /// carrying the person's token, the secret the server generated is answered once,
    /// and what the library writes, the notice and the audit row, carries neither the
    /// secret nor the label.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_002_AC1_TheServerGeneratesTheSecretAndTheLibraryKeepsNoneAsync()
    {
        IssuedAppPassword issued = Issued(await CreateAsync("Phone", _session));

        Assert.Equal(Assert.Single(_server.Secrets), issued.Secret);
        Assert.Equal(Assert.Single(_tokens.Issued), Assert.Single(_server.Tokens));
        Assert.Equal("Phone", Assert.Single(_server.AppPasswordsOf(_person)).Label);
        Assert.Equal(
            (AuditActions.MailCredentialCreated, _person, issued.Id),
            Assert.Single(_audit.MailCredentials));

        SendRequest notice = Assert.Single(_notifications.Sent);

        Assert.Equal(MessageKind.SecurityNotice, notice.Message);
        Assert.Empty(notice.Values);
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// REG-MAIL-002 AC2: creation and revocation answer the step-up code to a session
    /// that has not stepped up, and nothing reaches the server, the notice set or the
    /// trail.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_002_AC2_CreationAndRevocationAskForAStepUpAsync()
    {
        SessionId stale = Opened(_person, Stale);

        Assert.Equal(ErrorCodes.StepUpRequired, Refused(await CreateAsync("Phone", stale)));
        Assert.Equal(ErrorCodes.StepUpRequired, Refused(await RevokeAsync("app-password-1", stale)));
        Assert.Empty(_server.Tokens);
        Assert.Empty(_notifications.Sent);
        Assert.Empty(_audit.MailCredentials);
    }

    /// <summary>
    /// REG-MAIL-002 AC2, INT-MAIL-010 AC3: the revocation removes the credential at the
    /// server, is notified and audited by the server's identifier, and one the server
    /// does not hold for the person is not found.
    /// </summary>
    [Fact]
    public async Task INT_MAIL_010_AC3_TheRevokedAppPasswordIsGoneAtTheServerAsync()
    {
        IssuedAppPassword issued = Issued(await CreateAsync("Phone", _session));

        Accepted(await RevokeAsync(issued.Id, _session));

        Assert.Empty(_server.AppPasswordsOf(_person));
        Assert.Equal(
            (AuditActions.MailCredentialRevoked, _person, issued.Id),
            _audit.MailCredentials[^1]);
        Assert.Equal(2, _notifications.Sent.Count);
        Assert.Equal(ErrorCodes.CredentialNotFound, Refused(await RevokeAsync(issued.Id, _session)));
        Assert.Equal(ErrorCodes.CredentialNotFound, Refused(await RevokeAsync(" ", _session)));
        Assert.Equal(2, _audit.MailCredentials.Count);
    }

    /// <summary>
    /// INT-MAIL-010 AC2: the listing is read from the server every time, so what the
    /// server holds is what is answered, including what the library never saw created.
    /// </summary>
    [Fact]
    public async Task INT_MAIL_010_AC2_TheListingIsWhatTheServerHoldsNowAsync()
    {
        _ = Issued(await CreateAsync("Phone", _session));

        IReadOnlyList<AppPassword> first = Listed(await ListAsync(_session));

        _ = await _server.CreateAppPasswordAsync(
            _tokens.Issued[0],
            "Laptop",
            Noon.AddDays(30),
            TestContext.Current.CancellationToken);

        IReadOnlyList<AppPassword> second = Listed(await ListAsync(_session));

        Assert.Equal(["Phone"], first.Select(password => password.Label));
        Assert.Equal(["Phone", "Laptop"], second.Select(password => password.Label));
        Assert.Equal(Noon.AddDays(30), second[1].ExpiresAt);
        Assert.Equal(4, _server.Tokens.Count);
        Assert.Empty(_audit.MailCredentials.Skip(1));
    }

    /// <summary>
    /// INT-MAIL-006: the operations are present only where the account holds a mailbox
    /// the server enables, so an account without one, one whose mailbox was retired, an
    /// account that is not active and a deployment with no mail server are all refused.
    /// </summary>
    [Fact]
    public async Task INT_MAIL_006_WithoutAnEnabledMailboxThereAreNoAppPasswordsAsync()
    {
        var bare = SubjectId.New(_randomness);

        _accounts.Stands(bare, AccountState.Active);

        Assert.Equal(ErrorCodes.Denied, Refused(await Built(_server).ListAsync(
            AccessContext.Of(bare),
            Opened(bare, Noon),
            TestContext.Current.CancellationToken)));
        Assert.Equal(ErrorCodes.Denied, Refused(await Built(server: null).ListAsync(
            Asking,
            _session,
            TestContext.Current.CancellationToken)));

        _accounts.Stands(_person, AccountState.Restricted);

        Assert.Equal(ErrorCodes.Denied, Refused(await CreateAsync("Phone", _session)));

        _accounts.Stands(_person, AccountState.Active);
        Assert.Single(_mailboxes.Held).Retire(Noon);

        Assert.Equal(ErrorCodes.Denied, Refused(await RevokeAsync("app-password-1", _session)));
        Assert.Empty(_server.Tokens);
    }

    /// <summary>
    /// AUTH-OIDC-001 AC4: the token stands on the session named, which has to be live
    /// and the person's own; a session that is not issues nothing and the server is
    /// never called.
    /// </summary>
    [Fact]
    public async Task AUTH_OIDC_001_AC4_OnlyThePersonsLiveSessionObtainsATokenAsync()
    {
        var other = SubjectId.New(_randomness);

        Assert.Equal(ErrorCodes.Denied, Refused(await ListAsync(Opened(other, Noon))));

        await _sessions.EndSpineAsync(_session, Noon, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SessionExpired, Refused(await ListAsync(_session)));
        Assert.Empty(_tokens.Issued);
        Assert.Empty(_server.Tokens);
    }

    /// <summary>
    /// REG-MAIL-002: each app password carries a label, bounded as a credential's is.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_002_AnAppPasswordCarriesALabelAsync()
    {
        Assert.Equal(ErrorCodes.CredentialLabelInvalid, Refused(await CreateAsync(" ", _session)));
        Assert.Equal(ErrorCodes.CredentialLabelInvalid, Refused(await CreateAsync(new string('a', 65), _session)));
        Assert.Empty(_server.Tokens);
    }

    private static void Accepted(Result outcome) =>
        outcome.Switch(() => { }, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static IssuedAppPassword Issued(Result<IssuedAppPassword> outcome) =>
        outcome.Match(issued => issued, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static IReadOnlyList<AppPassword> Listed(Result<IReadOnlyList<AppPassword>> outcome) =>
        outcome.Match(held => held, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static ErrorCode Refused(Result outcome) =>
        outcome.Match<ErrorCode>(
            () => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

    private static ErrorCode Refused<T>(Result<T> outcome) =>
        outcome.Match<ErrorCode>(
            _ => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

    private AppPasswords Built(IMailServer? server) =>
        new(
            server,
            _tokens,
            _mailboxes,
            _accounts,
            new StepUpGuard(
                _sessions,
                _authenticators,
                _passwords,
                new PolicyResolution(_memberships, _configuration, _raises),
                _clock),
            _identifiers,
            _notifications,
            _configuration,
            _audit,
            _work,
            _clock);

    private async Task<Result<IssuedAppPassword>> CreateAsync(string label, SessionId session) =>
        await Passwords.CreateAsync(Asking, session, label, expiresAt: null, Source, TestContext.Current.CancellationToken);

    private async Task<Result> RevokeAsync(string id, SessionId session) =>
        await Passwords.RevokeAsync(Asking, session, id, Source, TestContext.Current.CancellationToken);

    private async Task<Result<IReadOnlyList<AppPassword>>> ListAsync(SessionId session) =>
        await Passwords.ListAsync(Asking, session, TestContext.Current.CancellationToken);

    private SessionId Opened(SubjectId subject, DateTimeOffset at)
    {
        var session = Session.Begin(
            SessionId.New(_clock),
            subject,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Somewhere,
            at,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            satisfiesEveryGate: false);
        byte[] fingerprint = new byte[32];
        byte[] synchronizer = new byte[32];

        _randomness.GetBytes(fingerprint);
        _randomness.GetBytes(synchronizer);
        _sessions.AddAsync(session, fingerprint, synchronizer, TestContext.Current.CancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return session.Id;
    }

    private static EmailAddress Parsed(string value)
    {
        Assert.True(EmailAddress.TryParse(value, out EmailAddress address));

        return address;
    }
}
