using System;
using System.Linq;
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
/// (CONV-DESIGN-003), and what the lock admits once they are made (REG-DOM-001).
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
    /// CONV-DESIGN-002 (D-207): a domain added where the deployment declares no resolver
    /// is refused before the step-up is judged and before any unit of work begins.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_002_ADomainAddedWithNoResolverIsRefusedBeforeItsUnitOfWorkAsync()
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
        Assert.Equal(0, _work.Opened);
        Assert.Equal(0, _work.RolledBack);
        Assert.Empty(_domains.Held);
    }

    /// <summary>
    /// CONV-DESIGN-002 (D-207): a domain added on a session whose proof is not recent is
    /// refused at its step-up, judged before any unit of work begins.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_002_ADomainAddedWithoutStepUpIsRefusedBeforeItsUnitOfWorkAsync()
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
        Assert.Equal(0, _work.Opened);
        Assert.Equal(0, _work.RolledBack);
        Assert.Empty(_domains.Held);
    }

    /// <summary>
    /// CONV-DESIGN-002 (D-207): a domain removed on a session whose proof is not recent
    /// is refused at its step-up, judged before any unit of work begins.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_002_ADomainRemovedWithoutStepUpIsRefusedBeforeItsUnitOfWorkAsync()
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
        Assert.Equal(0, _work.Opened);
        Assert.Equal(0, _work.RolledBack);
        Assert.True(Assert.Single(_domains.Held).IsListed);
    }

    /// <summary>
    /// REG-DOM-001 AC5: with only unverified domains listed the lock is on and admits
    /// nothing, so a member's sign-in email is refused whatever its domain: one in a
    /// listed domain and one in a domain never listed alike.
    /// </summary>
    [Fact]
    public async Task REG_DOM_001_AC5_WithOnlyUnverifiedDomainsListedEveryAddressIsRefusedAsync()
    {
        SubjectId member = Member();
        _ = await ListedAsync(Domain);
        _ = await ListedAsync("corp.example");

        ErrorCode? listed = await JudgedAsync(member, "person@example.test");
        ErrorCode? listedToo = await JudgedAsync(member, "person@corp.example");
        ErrorCode? unlisted = await JudgedAsync(member, "person@elsewhere.test");

        Assert.All(_domains.Held, held => Assert.Null(held.VerifiedAt));
        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed, listed);
        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed, listedToo);
        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed, unlisted);
    }

    /// <summary>
    /// REG-DOM-001 AC6: once the last listed domain is removed the list is empty, an
    /// address in the removed domain is still refused, and an address in any other
    /// domain, refused while the domain was listed, is admitted.
    /// </summary>
    [Fact]
    public async Task REG_DOM_001_AC6_AfterTheLastDomainIsRemovedItsAddressesAreRefusedAndOthersAdmittedAsync()
    {
        SubjectId member = Member();
        await VerifiedAsync(await ListedAsync(Domain));
        ErrorCode? outsideWhileListed = await JudgedAsync(member, "person@elsewhere.test");

        await RemovedAsync(Domain);
        ErrorCode? within = await JudgedAsync(member, "person@example.test");
        ErrorCode? outside = await JudgedAsync(member, "person@elsewhere.test");

        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed, outsideWhileListed);
        Assert.DoesNotContain(_domains.Held, held => held.IsListed);
        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed, within);
        Assert.Null(outside);
    }

    /// <summary>
    /// REG-DOM-001 AC7: a domain listed anew draws a token no row of it ever carried,
    /// and admits nothing, the record of its first listing still published, until its
    /// own record is found.
    /// </summary>
    [Fact]
    public async Task REG_DOM_001_AC7_ADomainListedAnewDrawsANewTokenAndAdmitsNothingUntilVerifiedAsync()
    {
        SubjectId member = Member();
        OrganizationDomain first = await ListedAsync(Domain);
        await VerifiedAsync(first);
        await RemovedAsync(Domain);

        OrganizationDomain anew = await ListedAsync(Domain);
        ErrorCode? unverified = await JudgedAsync(member, "person@example.test");
        await VerifiedAsync(anew);
        ErrorCode? verified = await JudgedAsync(member, "person@example.test");

        Assert.NotEqual(first.RecordValue, anew.RecordValue);
        Assert.Equal(2, _domains.Held.Select(held => held.Token).Distinct(StringComparer.Ordinal).Count());
        Assert.Null(anew.VerifiedAt);
        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed, unverified);
        Assert.Null(verified);
    }

    private static DateTimeOffset Stale =>
        Noon - Settings.SessionStepUpRecency.Default - TimeSpan.FromMinutes(1);

    private static TValue Admitted<TValue>(Result<TValue> outcome) =>
        outcome.Match(
            value => value,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    // A member of the organization whose list the tests change.
    private SubjectId Member()
    {
        var member = SubjectId.New(_randomness);

        _memberships.Place(member, Customer);

        return member;
    }

    private async Task<OrganizationDomain> ListedAsync(string domain) =>
        Admitted(await Service(_dns).AddDomainAsync(
            Acting,
            Opened(Noon),
            Customer,
            domain,
            Reason,
            TestContext.Current.CancellationToken));

    // The domain's record published as the listing answered it, and the domain verified.
    private async Task VerifiedAsync(OrganizationDomain listed)
    {
        _dns.Publish(listed.RecordName, listed.RecordValue);

        _ = Admitted(await Service(_dns).VerifyDomainAsync(
            Acting,
            Opened(Noon),
            Customer,
            listed.Domain,
            Reason,
            TestContext.Current.CancellationToken));
    }

    private async Task RemovedAsync(string domain) =>
        (await Service(_dns).RemoveDomainAsync(
                Acting,
                Opened(Noon),
                Customer,
                domain,
                Reason,
                TestContext.Current.CancellationToken))
            .Match(
                () => true,
                error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    // What the lock answers a member's sign-in email: the refusal's code, or nothing
    // where it admits the address.
    private async Task<ErrorCode?> JudgedAsync(SubjectId member, string address) =>
        (await new DomainLock(_memberships, _configuration, _domains)
            .RefusedAsync(member, address, TestContext.Current.CancellationToken))?.Code;

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
