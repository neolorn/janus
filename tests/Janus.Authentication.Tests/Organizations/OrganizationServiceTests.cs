using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Organizations;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Configuration;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Organizations;

/// <summary>
/// What an administrator does to an organization, as the unit of work sees it ended
/// (CONV-DESIGN-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationServiceTests : IAsyncDisposable
{
    private const string Reason = "the contract ended";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Staff = new(Guid.NewGuid());

    private static readonly OrganizationId Customer = new(Guid.NewGuid());

    private static readonly SessionOrigin Somewhere = new("198.51.100.7", new DeviceDescription("Firefox", "Fedora"));

    private readonly MembershipLookupInMemory _memberships = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new() { Organization = Staff };
    private readonly SessionStoreInMemory _sessions = new();
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly ConfigurationAuditInMemory _changes = new();
    private readonly OrganizationAuditInMemory _audit = new();
    private readonly EventsInMemory _events = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly OrganizationsInMemory _organizations;
    private readonly SubjectId _administrator;

    /// <summary>
    /// A deployment with its administrative organization and one other, and an
    /// administrator who may manage organizations.
    /// </summary>
    public OrganizationServiceTests()
    {
        _organizations = new OrganizationsInMemory(_memberships);
        _organizations.Seed(Staff, administrative: true);
        _organizations.Seed(Customer);

        _administrator = SubjectId.New(_randomness);
        _passwords.Hold(_administrator, Noon);
        _gate.Grant(_administrator, Staff, Permissions.OrganizationManage);
    }

    private OrganizationService Service
    {
        get
        {
            var scope = new AdministrativeScope(_gate, _administrative);
            var policies = new PolicyResolution(_memberships, _configuration, _raises);

            return new OrganizationService(
                scope,
                new StepUpGuard(
                    _sessions,
                    _authenticators,
                    _passwords,
                    policies,
                    new IdentifierDirectoryInMemory(),
                    new PhoneSignals(null, new PhoneSignalAuditInMemory(), _work, _clock),
                    _clock),
                _organizations,
                _sessions,
                _audit,
                _configuration,
                new ConfigurationAdministration(
                    _configuration,
                    _configuration,
                    _changes,
                    scope,
                    policies,
                    new RelayRegistration(_configuration, _events, _clock),
                    _events,
                    _work,
                    _clock),
                policies,
                _events,
                new ImageCodecInMemory().Declared,
                _work,
                _clock);
        }
    }

    private AccessContext Acting => AccessContext.Of(_administrator);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a deletion request the directory refuses under the
    /// organization's lock, for an organization made the administrative one since it
    /// was first judged, rolls its unit of work back and commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ADeletionRequestRefusedUnderTheLockIsRolledBackAsync()
    {
        _organizations.Holding = organization => _organizations.Seed(organization, administrative: true);

        Result refused = await Service.RequestDeletionAsync(
            Acting,
            Stepped(),
            Customer,
            Reason,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.OrganizationProtected, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_audit.Changes);
    }


    /// <summary>
    /// CONV-DESIGN-003 AC10: a deletion request that finds one made under the
    /// organization's lock, and a cancellation that finds the deletion cancelled, are
    /// each done having written nothing, so each rolls its unit of work back.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_AChangeMadeMeanwhileIsRolledBackAsync()
    {
        _organizations.Holding = organization => _organizations.Seed(organization, deletionRequestedAt: Noon);

        Result requested = await Service.RequestDeletionAsync(
            Acting,
            Stepped(),
            Customer,
            Reason,
            TestContext.Current.CancellationToken);

        Assert.True(requested.Match(() => true, _ => false));
        Assert.Equal((0, 1, false), (_work.Committed, _work.RolledBack, _work.Open));
        Assert.Empty(_audit.Changes);

        _organizations.Holding = organization => _organizations.Seed(organization);
        _work.Reset();

        Result cancelled = await Service.CancelDeletionAsync(
            Acting,
            Stepped(),
            Customer,
            Reason,
            TestContext.Current.CancellationToken);

        Assert.True(cancelled.Match(() => true, _ => false));
        Assert.Equal((0, 1, false), (_work.Committed, _work.RolledBack, _work.Open));
        Assert.Empty(_audit.Changes);
    }
    /// <summary>
    /// CONV-DESIGN-003 AC5: a cancellation that finds the organization erased under its
    /// lock rolls its unit of work back and commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ACancellationRefusedUnderTheLockIsRolledBackAsync()
    {
        _organizations.Seed(Customer, deletionRequestedAt: Noon);
        _organizations.Holding = organization =>
            _organizations.Seed(organization, deletionRequestedAt: Noon, erasedAt: Noon);

        Result refused = await Service.CancelDeletionAsync(
            Acting,
            Stepped(),
            Customer,
            Reason,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.DeletionWindowElapsed, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_audit.Changes);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a policy that would take the administrative organization
    /// below its floor, decided under the rows' locks, rolls its unit of work back and
    /// commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_APolicyRefusedUnderTheLocksIsRolledBackAsync()
    {
        Result refused = await Service.ReplacePolicyAsync(
            Acting,
            Stepped(),
            Staff,
            PolicyOverride.None,
            Reason,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ConfigurationValueBelowFloor, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_changes.Written);
    }

    private static ErrorCode Refused(Result outcome) =>
        outcome.Match<ErrorCode>(
            () => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

    private SessionId Stepped()
    {
        var session = Session.Begin(
            SessionId.New(_clock),
            _administrator,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Somewhere,
            Noon,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        _sessions.AddAsync(session, Drawn(), Drawn(), TestContext.Current.CancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return session.Id;
    }

    private byte[] Drawn()
    {
        byte[] fingerprint = new byte[32];

        _randomness.GetBytes(fingerprint);

        return fingerprint;
    }
}
