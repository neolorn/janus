using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Organizations;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
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
/// The changes to an organization's domain list, as the unit of work sees them ended
/// (CONV-DESIGN-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationDomainServiceTests : IAsyncDisposable
{
    private const string Domain = "example.test";
    private const string Reason = "the organization's own domain";

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
    private readonly DomainStoreInMemory _domains = new();
    private readonly DnsResolverInMemory _dns = new();
    private readonly EventsInMemory _events = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly OrganizationsInMemory _organizations;
    private readonly SubjectId _administrator;

    /// <summary>
    /// A deployment with its administrative organization and one other, and an
    /// administrator who may manage domains and loosen the deployment.
    /// </summary>
    public OrganizationDomainServiceTests()
    {
        _organizations = new OrganizationsInMemory(_memberships);
        _organizations.Seed(Staff, administrative: true);
        _organizations.Seed(Customer);

        _administrator = SubjectId.New(_randomness);
        _passwords.Hold(_administrator, Noon);
        _gate.Grant(_administrator, Staff, Permissions.DomainManage);
        _gate.Grant(_administrator, Staff, Permissions.SystemAdminister);
    }

    private AccessContext Acting => AccessContext.Of(_administrator);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a domain added where the deployment declares no resolver is
    /// refused after the list's row is held, and the refusal rolls the unit of work back.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ADomainAddedWithNoResolverIsRolledBackAsync()
    {
        Result<OrganizationDomain> refused = await Service(dns: null).AddDomainAsync(
            Acting,
            Opened(Noon),
            Customer,
            Domain,
            Reason,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_domains.Held);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a domain added on a session whose proof is not recent is
    /// refused under the list's lock, and the refusal rolls the unit of work back.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ADomainAddedWithoutStepUpIsRolledBackAsync()
    {
        Result<OrganizationDomain> refused = await Service(_dns).AddDomainAsync(
            Acting,
            Opened(Stale),
            Customer,
            Domain,
            Reason,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.StepUpRequired, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_domains.Held);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a domain removed on a session whose proof is not recent is
    /// refused under the list's lock, and the refusal rolls the unit of work back.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ADomainRemovedWithoutStepUpIsRolledBackAsync()
    {
        _ = (await Service(_dns).AddDomainAsync(
                Acting,
                Opened(Noon),
                Customer,
                Domain,
                Reason,
                TestContext.Current.CancellationToken))
            .Match(added => added, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));
        _work.Reset();

        Result refused = await Service(_dns).RemoveDomainAsync(
            Acting,
            Opened(Stale),
            Customer,
            Domain,
            Reason,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.StepUpRequired,
            refused.Match<ErrorCode>(
                () => throw new Xunit.Sdk.XunitException("The operation was admitted."),
                error => error.Code));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.True(Assert.Single(_domains.Held).IsListed);
    }

    private static DateTimeOffset Stale =>
        Noon - Settings.SessionStepUpRecency.Default - TimeSpan.FromMinutes(1);

    private static ErrorCode Refused<TValue>(Result<TValue> outcome) =>
        outcome.Match<ErrorCode>(
            _ => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

    private OrganizationDomainService Service(IDnsResolver? dns)
    {
        var scope = new AdministrativeScope(_gate, _administrative);
        var policies = new PolicyResolution(_memberships, _configuration, _raises);

        return new OrganizationDomainService(
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
            _domains,
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
            dns,
            _audit,
            _events,
            _work,
            _clock,
            _randomness);
    }

    private SessionId Opened(DateTimeOffset at)
    {
        var session = Session.Begin(
            SessionId.New(_clock),
            _administrator,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Somewhere,
            at,
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
