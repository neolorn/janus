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
using Janus.Core.Configuration;
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
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events;
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
        _events = new EventsInMemory { Work = _work };
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
    /// IDN-ORG-003 AC8: the sessions of the organization's current members end inside
    /// the unit of work that suspends it: nothing has ended when the organization's row
    /// is held, and the one commit that follows carries the suspension, the ended
    /// sessions and the record together. A session of someone who is no member stands.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_AC8_TheMemberSessionsEndInTheUnitOfWorkThatSuspendsAsync()
    {
        var member = SubjectId.New(_randomness);
        Session theirs = Begun(member);
        Session own = Begun(_administrator);
        (bool Open, DateTimeOffset? Ended) held = default;

        _memberships.Place(member, Customer);
        _organizations.Holding = _ => held = (_work.Open, theirs.EndedAt);

        Result requested = await Service.RequestDeletionAsync(
            Acting,
            own.Id,
            Customer,
            Reason,
            TestContext.Current.CancellationToken);

        Assert.True(requested.Match(() => true, _ => false));
        Assert.Equal((true, null), held);
        Assert.Equal((1, 0, false), (_work.Committed, _work.RolledBack, _work.Open));
        Assert.Equal(Noon, theirs.EndedAt);
        Assert.Null(own.EndedAt);
        Assert.Equal(
            Noon,
            (await _organizations.FindAsync(Customer, TestContext.Current.CancellationToken))?.DeletionRequestedAt);
        Assert.Single(_audit.Changes);
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
    /// CONV-DESIGN-003 AC5: a policy judged within the system policy before its unit of
    /// work begins, and below it once the rows are held because the system policy was
    /// raised meanwhile, is refused under the rows' locks, rolls its unit of work back
    /// and commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_APolicyRefusedUnderTheLocksIsRolledBackAsync()
    {
        _work.Meanwhile = () => _configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with { RequiredAssurance = AssuranceLevel.Aal2 });

        Result refused = await Service.ReplacePolicyAsync(
            Acting,
            Stepped(),
            Customer,
            PolicyOverride.None with { RequiredAssurance = AssuranceLevel.Aal1 },
            Reason,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ConfigurationPolicyBelowSystem, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_changes.Written);
    }

    /// <summary>
    /// CONV-DESIGN-002 (D-207): a policy that would take the administrative organization
    /// below its floor is refused before its step-up is judged and before any unit of
    /// work begins.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_002_APolicyBelowTheFloorIsRefusedBeforeItsUnitOfWorkAsync()
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
        Assert.Equal(0, _work.Opened);
        Assert.Equal(0, _work.RolledBack);
        Assert.Empty(_changes.Written);
    }

    /// <summary>
    /// LIB-API-001, chapter 10 section 5b (D-218): a created organization is announced
    /// once, in the transaction that creates it, naming the organization and no subject,
    /// with the administrator who acted.
    /// </summary>
    [Fact]
    public async Task LIB_API_001_ACreatedOrganizationIsAnnouncedInItsTransactionAsync()
    {
        Result<OrganizationId> created = await Service.CreateAsync(
            Acting,
            "Northwind Freight",
            Reason,
            TestContext.Current.CancellationToken);

        OrganizationId organization = created.Match(
            id => id,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));
        OrganizationCreated announced = Assert.Single(_events.Of<OrganizationCreated>());

        Assert.Equal(organization, announced.Organization);
        Assert.Equal(Noon, announced.RaisedAt);
        Assert.Null(announced.Subject);
        Assert.Equal((_administrator, _administrator), (announced.Actor, announced.Effective));
        Assert.Contains(announced, _events.PublishedInTransaction);
    }

    /// <summary>
    /// LIB-API-001, chapter 10 section 5b (D-218): a deletion request and its
    /// cancellation are each announced once, in the transaction that makes it true, and a
    /// request for an organization whose deletion is already requested announces nothing.
    /// </summary>
    [Fact]
    public async Task LIB_API_001_ADeletionRequestAndItsCancellationAreAnnouncedInTheirTransactionsAsync()
    {
        Assert.True((await Service.RequestDeletionAsync(Acting, Stepped(), Customer, Reason, TestContext.Current.CancellationToken))
            .Match(() => true, _ => false));
        Assert.True((await Service.RequestDeletionAsync(Acting, Stepped(), Customer, Reason, TestContext.Current.CancellationToken))
            .Match(() => true, _ => false));

        OrganizationDeletionRequested requested = Assert.Single(_events.Of<OrganizationDeletionRequested>());

        Assert.Equal((Customer, (SubjectId?)null), (requested.Organization, requested.Subject));
        Assert.Contains(requested, _events.PublishedInTransaction);

        Assert.True((await Service.CancelDeletionAsync(Acting, Stepped(), Customer, Reason, TestContext.Current.CancellationToken))
            .Match(() => true, _ => false));

        OrganizationDeletionCancelled cancelled = Assert.Single(_events.Of<OrganizationDeletionCancelled>());

        Assert.Equal((Customer, (SubjectId?)null), (cancelled.Organization, cancelled.Subject));
        Assert.Equal(_administrator, cancelled.Actor);
        Assert.Contains(cancelled, _events.PublishedInTransaction);
        Assert.NotEqual(requested.IdempotencyKey, cancelled.IdempotencyKey);
    }

    /// <summary>
    /// CONV-DESIGN-002 (D-218): an organization whose event is not taken is not created:
    /// the unit of work rolls back with the failure, and nothing is recorded.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_002_AnOrganizationWhoseEventIsNotTakenIsNotCreatedAsync()
    {
        _events.Refusal = Error.From(ErrorCodes.SystemFault);

        Result<OrganizationId> created = await Service.CreateAsync(
            Acting,
            "Northwind Freight",
            Reason,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.SystemFault,
            created.Match(_ => throw new Xunit.Sdk.XunitException("The organization was created."), error => error.Code));
        Assert.Equal((1, false), (_work.RolledBack, _work.Open));
        Assert.Empty(_audit.Changes);
    }

    private static ErrorCode Refused(Result outcome) =>
        outcome.Match<ErrorCode>(
            () => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

    private SessionId Stepped() => Begun(_administrator).Id;

    private Session Begun(SubjectId subject)
    {
        var session = Session.Begin(
            SessionId.New(_clock),
            subject,
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

        return session;
    }

    private byte[] Drawn()
    {
        byte[] fingerprint = new byte[32];

        _randomness.GetBytes(fingerprint);

        return fingerprint;
    }
}
