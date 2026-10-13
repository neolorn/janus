using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Oidc;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.BreakGlass;

/// <summary>
/// How a presentation and a generation of the break-glass credential end the unit of
/// work they run in (CONV-DESIGN-003, OPS-BOOT-004, CONV-LOG-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class BreakGlassServiceTests : IAsyncDisposable
{
    private const string Reason = "The identity provider of the staff is unreachable.";

    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Administration = new(Guid.NewGuid());

    private static readonly SessionOrigin Somewhere = new("198.51.100.7", new DeviceDescription("Firefox", "Fedora"));

    private readonly BreakGlassStoreInMemory _store = new();
    private readonly EmergencyAccountInMemory _emergency = new();
    private readonly BreakGlassAuditInMemory _recorded = new();
    private readonly SessionAuditInMemory _audit = new();
    private readonly SessionStoreInMemory _live = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly AccountDirectoryInMemory _accounts = new(PreferenceDeclarations.None);
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly CredentialAuditInMemory _credentials = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new() { Organization = Administration };
    private readonly LocationResolverInMemory _locations = new();
    private readonly ThrottleLedgerInMemory _throttle = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly PhoneSignalAuditInMemory _considered = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly EventsInMemory _alerts = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, OPS-BOOT-002 AC3: a use whose alert cannot be raised fails
    /// after the credential was spent and the session opened, and the failure ends the
    /// one unit of work of the presentation with nothing committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AUseThatCannotBeAlertedRollsBackAsync()
    {
        string code = await IssuedAsync();

        _alerts.Refusal = Error.From(ErrorCodes.SystemFault);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refused(await Service.PresentAsync(code, Reason, Somewhere, TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, OPS-BOOT-004 AC2: a generation whose alert cannot be raised
    /// is refused after the issue was written, and the refusal ends the unit of work
    /// with nothing committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AGenerationThatCannotBeAlertedRollsBackAsync()
    {
        var administrator = new SubjectId(Guid.NewGuid());

        _accounts.Stands(administrator, AccountState.Active);
        _passwords.Hold(administrator, Noon);
        _memberships.Place(administrator, Administration);
        _gate.Grant(administrator, Administration, Permissions.SystemAdminister);

        IssuedSession session = (await Sessions.BeginAsync(
                administrator,
                [Factor.Password],
                Somewhere,
                TestContext.Current.CancellationToken))
            .Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        _alerts.Refusal = Error.From(ErrorCodes.SystemFault);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refused(await Service.GenerateAsync(
                AccessContext.Of(administrator),
                session.Id,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, OPS-BOOT-004, CONV-LOG-005: a refused code is refused with
    /// its attempt counted, its source's failure counted and its failed authentication
    /// recorded, committed together by the one unit of work that decided the refusal.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ARefusedCodeCommitsItsKeptWritesTogetherAsync()
    {
        _ = await IssuedAsync();

        _work.Reset();

        Assert.Equal(
            ErrorCodes.BreakGlassInvalid,
            Refused(await Service.PresentAsync(
                BreakGlassCode.Draw(_randomness),
                Reason,
                Somewhere,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal(1, _work.OutermostCommitted);
        _ = Assert.Single(_store.Attempts);
        Assert.Contains(_throttle.Counted, counted => counted.Scope is ThrottleScope.Source);
        Assert.Equal((_emergency.Account, Factor.BreakGlass), Assert.Single(_audit.Failed));
        Assert.Empty(_recorded.Used);
    }

    /// <summary>
    /// OPS-BOOT-004, CONV-LOG-005: the code of the issue last used, presented again, is
    /// a refused credential like any other: one unit of work decides the refusal and
    /// commits the attempt, the source's failure and the failed authentication with it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_004_AConsumedCodeIsARefusedCredentialAsync()
    {
        string code = await IssuedAsync();

        _ = await Service.PresentAsync(code, Reason, Somewhere, TestContext.Current.CancellationToken);

        Assert.Single(_recorded.Used);

        _work.Reset();

        Assert.Equal(
            ErrorCodes.BreakGlassConsumed,
            Refused(await Service.PresentAsync(code, Reason, Somewhere, TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(2, _store.Attempts.Count);
        Assert.Contains(_throttle.Counted, counted => counted.Scope is ThrottleScope.Source);
        Assert.Equal((_emergency.Account, Factor.BreakGlass), Assert.Single(_audit.Failed));
    }

    /// <summary>
    /// OPS-BOOT-004 AC7, CONV-DESIGN-003 AC10: an attempt the global limit refuses is
    /// counted, and the first one raises <c>auth-failures-sustained</c>, the count and
    /// the raise committed together with the refusal; no code is looked at.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_004_AC7_ARefusalByTheLimitCommitsItsCountAndItsRaiseTogetherAsync()
    {
        _ = await IssuedAsync();

        for (int arrived = 0; arrived < 5; arrived++)
        {
            _store.Attempts.Add(Noon);
        }

        int read = _store.Reads;

        _work.Reset();

        Assert.Equal(
            ErrorCodes.Throttled,
            Refused(await Service.PresentAsync(
                BreakGlassCode.Draw(_randomness),
                Reason,
                Somewhere,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal((1, 1, 0), (_work.Opened, _work.OutermostCommitted, _work.RolledBack));
        Assert.Equal(6, _store.Attempts.Count);
        Assert.Equal(read, _store.Reads);
        Assert.Equal(
            AlertCondition.AuthFailuresSustained,
            Assert.Single(_alerts.Of<AlertRaised>()).Condition);
        Assert.Empty(_audit.Failed);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10: where the raise of the first attempt the limit refuses
    /// cannot be written, the presentation fails and its unit of work is rolled back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ALimitWhoseRaiseIsNotWrittenRollsBackAsync()
    {
        _ = await IssuedAsync();

        for (int arrived = 0; arrived < 5; arrived++)
        {
            _store.Attempts.Add(Noon);
        }

        _alerts.Refusal = Error.From(ErrorCodes.SystemFault);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refused(await Service.PresentAsync(
                BreakGlassCode.Draw(_randomness),
                Reason,
                Somewhere,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal((0, 1), (_work.OutermostCommitted, _work.RolledBack));
    }

    private BreakGlassService Service =>
        new(
            _store,
            _emergency,
            _recorded,
            _audit,
            _alerts,
            Sessions,
            new ThrottleService(_configuration, _throttle, _work, _events, _clock),
            new AdministrativeScope(_gate, _administrative),
            Guard,
            new Argon2idHasher(_randomness),
            _configuration,
            new AuthenticationAddresses("https://signin.example.test", "https://id.example.test"),
            _work,
            _clock,
            _randomness);

    private PolicyResolution Policies => new(_memberships, _configuration, _raises);

    private StepUpGuard Guard =>
        new(
            _live,
            _authenticators,
            _passwords,
            Policies,
            _identifiers,
            new PhoneSignals(null, _considered, _work, _clock),
            _clock);

    private SessionService Sessions =>
        new(
            _live,
            _audit,
            _authenticators,
            _credentials,
            Policies,
            _configuration,
            new AdministrativeScope(_gate, _administrative),
            Guard,
            _accounts,
            _locations,
            new ConcurrentSessions(_live, _configuration, _events),
            new OidcClientStoreInMemory(),
            _work,
            _clock,
            _randomness);

    private static ErrorCode Refused<TValue>(Result<TValue> result) =>
        result.Match(_ => default, error => error.Code);

    // A standing credential of the reserved account, issued as a generation issues one,
    // and the code that spends it.
    private async ValueTask<string> IssuedAsync()
    {
        var account = new SubjectId(Guid.NewGuid());

        _accounts.Stands(account, AccountState.Active);
        _emergency.Account = account;

        string code = BreakGlassCode.Draw(_randomness);
        byte[] presented = BreakGlassCode.Presented(BreakGlassCode.Checked(code)!);

        await _store.AddAsync(
            BreakGlassCredential.Issue(
                new Argon2idHasher(_randomness).Hash(presented, new Argon2StrengthClass(19456, 2), 1),
                account,
                Noon),
            TestContext.Current.CancellationToken);

        return code;
    }
}
