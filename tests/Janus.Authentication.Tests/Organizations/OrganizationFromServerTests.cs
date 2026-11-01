using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Authentication.Organizations;
using Janus.Authentication.Policies;
using Janus.Authentication.Tests.Configuration;
using Janus.Authentication.Tests.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Organizations;

/// <summary>
/// What the <c>organization</c> command does to a deployment from the server, as the
/// unit of work sees it ended (OPS-BOOT-001, IDN-ORG-002, D-248).
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationFromServerTests : IAsyncDisposable
{
    private const string Name = "Northwind Clinics";

    private const string Reason = "Production is set up.";

    private static readonly DateTimeOffset Noon = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Staff = new(Guid.NewGuid());

    // A policy that asks more than the system's on every field it states.
    private static readonly PolicyOverride Stricter = PolicyOverride.None with
    {
        RequiredAssurance = AssuranceLevel.Aal2,
        LoginFactors = new HashSet<Factor> { Factor.Passkey, Factor.Password, Factor.Totp },
        CredentialRedundancy = CredentialRedundancy.Enforced,
        SelfServiceRecovery = false,
        GrammaticalGender = true,
    };

    private readonly MembershipLookupInMemory _memberships = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new() { Organization = Staff };
    private readonly OrganizationAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly ProtectedSettingsInMemory _settings = new();
    private readonly ConfigurationAuditInMemory _changes = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events;
    private readonly FixedClock _clock = new(Noon);
    private readonly OrganizationsInMemory _organizations;

    /// <summary>
    /// A bootstrapped deployment: its administrative organization and nothing else.
    /// </summary>
    public OrganizationFromServerTests()
    {
        _events = new EventsInMemory { Work = _work };
        _organizations = new OrganizationsInMemory(_memberships);
        _organizations.Seed(Staff, administrative: true, name: "Administration");
        _configuration.Set(Settings.OrganizationPolicy, Staff.ToString(), Administrative);
    }

    // Chapter 10 section 4.1a, the administrative organization's override at bootstrap.
    private static PolicyOverride Administrative => PolicyOverride.None with
    {
        RequiredAssurance = AssuranceLevel.Aal2,
        LoginFactors = new HashSet<Factor> { Factor.Passkey },
        CredentialRedundancy = CredentialRedundancy.Enforced,
        SelfServiceRecovery = false,
        Photos = false,
        GrammaticalGender = true,
    };

    private OrganizationFromServer Command =>
        new(
            _administrative,
            _organizations,
            _audit,
            _configuration,
            _configuration,
            _settings,
            _changes,
            new PolicyResolution(_memberships, _configuration, _raises),
            _events,
            _events,
            _work,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// OPS-BOOT-001 AC10 (D-248): where no organization holds the name, the command
    /// creates one in one transaction, writes its policy key as the route that creates
    /// one does, writes the policy from the file and the setting asked for, records
    /// each change and the creation under the principal <c>organization</c> with the
    /// reason given, and announces the creation; it names no account at all.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC10_AnOrganizationIsCreatedWithItsPolicyUnderTheCommandsPrincipalAsync()
    {
        OrganizationId created = Succeeded(await Command.RunAsync(
            new OrganizationFromServerRequest(Name, Stricter, Reason, MultipleMemberships: true),
            TestContext.Current.CancellationToken));

        ConfigurationKey policy = Settings.OrganizationPolicy.For(created.ToString());
        (AuditAction Action, OrganizationId Organization, SystemPrincipal Principal) recorded = Assert.Single(_audit.Principals);

        Assert.Equal(Name, _organizations.NameOf(created));
        Assert.Equal(Settings.OrganizationPolicy.Write(Stricter), _settings.Written[policy]);
        Assert.Equal(
            Settings.OrganizationMultipleMemberships.Write(true),
            _settings.Written[Settings.OrganizationMultipleMemberships.Key]);
        Assert.Equal(
            [
                (policy, Settings.OrganizationPolicy.Write(PolicyOverride.None), Settings.OrganizationPolicy.Write(Settings.OrganizationPolicy.Default), false),
                (policy, Settings.OrganizationPolicy.Write(Settings.OrganizationPolicy.Default), Settings.OrganizationPolicy.Write(Stricter), false),
                (Settings.OrganizationMultipleMemberships.Key, "false", "true", true),
            ],
            _changes.Principals.Select(change => (change.Key, change.Before, change.After, change.Loosening)));
        Assert.All(_changes.Principals, change => Assert.Equal(("organization", Reason), (change.Principal, change.Reason)));
        Assert.Equal((AuditActions.OrganizationCreated, created, "organization"), (recorded.Action, recorded.Organization, recorded.Principal.Name));
        Assert.Equal("OPS-BOOT-001", recorded.Principal.Reason);
        Assert.Equal([Reason], _audit.Reasons);
        Assert.Equal(created, Assert.Single(_events.Of<OrganizationCreated>()).Organization);
        Assert.Equal(_events.Published, _events.PublishedInTransaction);
        Assert.Empty(_audit.Changes);
        Assert.Equal((1, 1, 0), (_work.Opened, _work.Committed, _work.RolledBack));
    }

    /// <summary>
    /// OPS-BOOT-001 AC11 (D-248): where one organization holds the name, the command
    /// creates nothing, and with the file and the setting as they stand it writes and
    /// records nothing and answers the organization; with another file it writes the
    /// policy alone, as a change from what stood.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC11_AnOrganizationThatStandsIsChangedOnlyWhereTheFileDiffersAsync()
    {
        var standing = new OrganizationId(Guid.NewGuid());
        ConfigurationKey policy = Settings.OrganizationPolicy.For(standing.ToString());

        _organizations.Seed(standing, name: Name);
        _configuration.Set(Settings.OrganizationPolicy, standing.ToString(), Stricter);

        OrganizationId same = Succeeded(await Command.RunAsync(
            new OrganizationFromServerRequest("  northwind clinics ", Stricter, Reason, MultipleMemberships: false),
            TestContext.Current.CancellationToken));

        Assert.Equal(standing, same);
        Assert.False(_settings.Written.ContainsKey(policy));
        Assert.False(_settings.Written.ContainsKey(Settings.OrganizationMultipleMemberships.Key));
        Assert.Empty(_changes.Principals);
        Assert.Empty(_audit.Principals);
        Assert.Empty(_events.Published);

        PolicyOverride wider = Stricter with { RequiredAssurance = null };

        OrganizationId changed = Succeeded(await Command.RunAsync(
            new OrganizationFromServerRequest(Name, wider, Reason, MultipleMemberships: null),
            TestContext.Current.CancellationToken));

        ConfigurationChange written = Assert.Single(_changes.Principals);

        Assert.Equal(standing, changed);
        Assert.Equal(
            (policy, Settings.OrganizationPolicy.Write(Stricter), Settings.OrganizationPolicy.Write(wider), true, "organization"),
            (written.Key, written.Before, written.After, written.Loosening, written.Principal));
        Assert.Empty(_audit.Principals);
        Assert.Empty(_events.Published);
    }

    /// <summary>
    /// OPS-BOOT-001 AC12 (D-248): a name two organizations hold names no one
    /// organization, so the command is refused naming <c>name</c>, rolls its unit of
    /// work back and writes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC12_ANameTwoOrganizationsHoldIsRefusedAsync()
    {
        _organizations.Seed(new OrganizationId(Guid.NewGuid()), name: Name);
        _organizations.Seed(new OrganizationId(Guid.NewGuid()), name: Name.ToUpperInvariant());

        Error refused = Refused(await Command.RunAsync(
            new OrganizationFromServerRequest(Name, Stricter, Reason, MultipleMemberships: true),
            TestContext.Current.CancellationToken));

        Assert.Equal((ErrorCodes.RequestMalformed, "name"), (refused.Code, refused.Details["member"].GetString()));
        AssertNothingWritten();
        Assert.Equal((1, 0, 1), (_work.Opened, _work.Committed, _work.RolledBack));
    }

    /// <summary>
    /// OPS-BOOT-001 AC13 (D-248): before bootstrap marked an administrative organization
    /// the command sets nothing up and is refused with <c>authz.denied</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC13_ADeploymentNotBootstrappedIsRefusedAsync()
    {
        _administrative.Organization = null;

        Error refused = Refused(await Command.RunAsync(
            new OrganizationFromServerRequest(Name, Stricter, Reason, MultipleMemberships: true),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Denied, refused.Code);
        AssertNothingWritten();
        Assert.Equal((1, 0, 1), (_work.Opened, _work.Committed, _work.RolledBack));
    }

    /// <summary>
    /// OPS-BOOT-001 AC13 (D-248): every change the command makes carries the reason, so
    /// an empty one is refused with <c>config.change.reasonrequired</c> before any
    /// transaction begins.
    /// </summary>
    /// <param name="reason">The reason given.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OPS_BOOT_001_AC13_AnEmptyReasonIsRefusedAsync(string reason)
    {
        Error refused = Refused(await Command.RunAsync(
            new OrganizationFromServerRequest(Name, Stricter, reason, MultipleMemberships: true),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.ConfigurationChangeReasonRequired, refused.Code);
        AssertNothingWritten();
        Assert.Equal(0, _work.Opened);
    }

    /// <summary>
    /// OPS-BOOT-001 AC13 and IDN-ORG-002 AC4 (D-248): a policy the policy route refuses
    /// is refused with that route's code: a field looser than the system's, photos with
    /// no image codec declared (the command reads no declaration), and the
    /// administrative organization's members resolving below AAL2. Nothing is written.
    /// </summary>
    /// <param name="refusal">Which refusal the case meets.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("belowsystem")]
    [InlineData("photos")]
    [InlineData("belowfloor")]
    public async Task IDN_ORG_002_AC4_APolicyThePolicyRouteRefusesIsRefusedWithItsCodeAsync(string refusal)
    {
        (string named, PolicyOverride policy, ErrorCode code, string field) = refusal switch
        {
            "belowsystem" => (Name, Stricter with { LoginFactors = new HashSet<Factor> { Factor.Password, Factor.EmailLink } }, ErrorCodes.ConfigurationPolicyBelowSystem, "loginFactors"),
            "photos" => (Name, Stricter with { Photos = true }, ErrorCodes.ConfigurationValueNotAllowed, "photos"),
            _ => ("Administration", Administrative with { RequiredAssurance = null }, ErrorCodes.ConfigurationValueBelowFloor, "requiredAssurance"),
        };

        Error refused = Refused(await Command.RunAsync(
            new OrganizationFromServerRequest(named, policy, Reason, MultipleMemberships: true),
            TestContext.Current.CancellationToken));

        Assert.Equal((code, field), (refused.Code, refused.Details["field"].GetString()));
        AssertNothingWritten();
        Assert.Equal(0, _work.Committed);
    }

    /// <summary>
    /// IDN-ORG-002 AC4 (D-248): the policy written from the server keeps the domain lock
    /// in force, as a replacement through the route does, and states what the file
    /// states beside it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_002_AC4_ThePolicyKeepsTheDomainLockInForceAsync()
    {
        var standing = new OrganizationId(Guid.NewGuid());
        PolicyOverride locked = PolicyOverride.None with { EmailDomains = ["northwind.example.test"], GrammaticalGender = true };

        _organizations.Seed(standing, name: Name);
        _configuration.Set(Settings.OrganizationPolicy, standing.ToString(), locked);

        _ = Succeeded(await Command.RunAsync(
            new OrganizationFromServerRequest(Name, Stricter, Reason, MultipleMemberships: null),
            TestContext.Current.CancellationToken));

        PolicyOverride stored = Settings.OrganizationPolicy
            .Read(standing.ToString(), _settings.Written[Settings.OrganizationPolicy.For(standing.ToString())])
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(["northwind.example.test"], stored.EmailDomains);
        Assert.Equal(AssuranceLevel.Aal2, stored.RequiredAssurance);
        Assert.True(stored.LoginFactors!.SetEquals(Stricter.LoginFactors!));
    }

    private void AssertNothingWritten()
    {
        Assert.Empty(_changes.Principals);
        Assert.Empty(_audit.Principals);
        Assert.Empty(_events.Published);
        Assert.DoesNotContain(_settings.Written.Keys, key => key.ToString().StartsWith("policy.", StringComparison.Ordinal));
        Assert.False(_settings.Written.ContainsKey(Settings.OrganizationMultipleMemberships.Key));
    }

    private static OrganizationId Succeeded(Result<OrganizationId> result) =>
        result.Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

    private static Error Refused(Result<OrganizationId> result) =>
        result.Match(value => throw new InvalidOperationException("Set up " + value), error => error);
}
