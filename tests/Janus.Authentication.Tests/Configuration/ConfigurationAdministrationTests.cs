using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Configuration;

/// <summary>
/// The one operation a runtime setting changes through: what the direction costs, what
/// is written down, and how it reads back (OPS-CFG-002, OPS-CFG-005), and what a change
/// to the sending domain, its relay declaration or the system policy warns of
/// (INT-MAIL-011).
/// </summary>
[Trait("kind", "unit")]
public sealed class ConfigurationAdministrationTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Recency = TimeSpan.FromMinutes(15);

    private static readonly StepUpChallenge Satisfied =
        new(StepUpOutcome.Satisfied, AssuranceLevel.Aal2, PhishingResistant: false, Recency, [], null);

    private static readonly StepUpChallenge Wanting =
        new(StepUpOutcome.Present, AssuranceLevel.Aal2, PhishingResistant: false, Recency, [[Factor.Totp]], null);

    private readonly ConfigurationInMemory _configuration = new();
    private readonly ConfigurationAuditInMemory _changes = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly EventsInMemory _events = new();
    private readonly OrganizationId _administering;

    /// <summary>
    /// A deployment with an administrative organization, in which a test grants the
    /// permission to loosen to whoever it has make a change, sending from a domain it
    /// declared as registered with the relay.
    /// </summary>
    public ConfigurationAdministrationTests()
    {
        _administering = OrganizationId.New(_clock);
        _administrative.Organization = _administering;
        _configuration.Set(Settings.NotificationEmailSendingDomain, "mail.example.test");
        _configuration.Set<IReadOnlySet<string>>(
            Settings.NotificationEmailRelayRegistered,
            new HashSet<string>(["mail.example.test"], StringComparer.Ordinal));
    }

    private ConfigurationAdministration Administration =>
        new(
            _configuration,
            _changes,
            new AdministrativeScope(_gate, _administrative),
            new PolicyResolution(new MembershipLookupInMemory(), _configuration, _raises),
            new RelayRegistration(_configuration, _events, _clock),
            _events,
            _work,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    // The system default with one step-up action's gate replaced.
    private static Policy Gated(StepUpAction action, Gate gate) =>
        Janus.Core.Policies.SystemDefault with
        {
            Gates = new Dictionary<StepUpAction, Gate>(Janus.Core.Policies.SystemDefault.Gates) { [action] = gate },
        };

    /// <summary>
    /// OPS-CFG-002 AC1: shortening a session timeout tightens the deployment, so it
    /// passes with no gate met and no reason written.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_002_AC1_ShorteningASessionTimeoutRequiresNoStepUpAsync()
    {
        await ChangedAsync(
            Settings.SessionAal2Inactivity,
            TimeSpan.FromMinutes(30),
            reason: null,
            Wanting);

        Assert.Equal(TimeSpan.FromMinutes(30), await InForceAsync(Settings.SessionAal2Inactivity));
        Assert.False(Assert.Single(_changes.Written).Loosening);
    }

    /// <summary>
    /// OPS-CFG-002 AC2: lengthening one loosens it, so the gate has to have been met.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_002_AC2_LengtheningOneRequiresStepUpAsync()
    {
        Error refusal = await RefusedAsync(
            Settings.SessionAal2Inactivity,
            TimeSpan.FromHours(6),
            "a support window",
            Wanting);

        Assert.Equal(ErrorCodes.StepUpRequired, refusal.Code);
        Assert.Empty(_changes.Written);
        Assert.Equal(Settings.SessionAal2Inactivity.Default, await InForceAsync(Settings.SessionAal2Inactivity));
    }

    /// <summary>
    /// OPS-CFG-002 AC2: and a written reason, which is named against the key it is
    /// wanted for.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_002_AC2_LengtheningOneRequiresAReasonAsync()
    {
        Error refusal = await RefusedAsync(
            Settings.SessionAal2Inactivity,
            TimeSpan.FromHours(6),
            reason: "   ",
            Satisfied);

        Assert.Equal(ErrorCodes.RestrictionReasonRequired, refusal.Code);
        Assert.Equal(
            Settings.SessionAal2Inactivity.Key.ToString(),
            refusal.Details["key"].GetString());
        Assert.Empty(_changes.Written);
    }

    /// <summary>
    /// OPS-CFG-002 AC3: a key with no direction is classified as loosening whichever
    /// way it moves, so replacing the alert destinations costs the gate and a reason.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_002_AC3_AChangeWithNoDirectionRequiresStepUpAndAReasonAsync()
    {
        _configuration.Set(Settings.AlertingEmailDestinations, ["one@example.test"]);

        Assert.Equal(
            ErrorCodes.StepUpRequired,
            (await RefusedAsync(
                Settings.AlertingEmailDestinations,
                ["two@example.test"],
                "an incident",
                Wanting)).Code);

        Assert.Equal(
            ErrorCodes.RestrictionReasonRequired,
            (await RefusedAsync(
                Settings.AlertingEmailDestinations,
                ["two@example.test"],
                reason: null,
                Satisfied)).Code);

        await ChangedAsync(
            Settings.AlertingEmailDestinations,
            ["two@example.test"],
            "an incident",
            Satisfied);

        Assert.True(Assert.Single(_changes.Written).Loosening);
    }

    /// <summary>
    /// OPS-CFG-002 AC4: both directions are audited, the free one included.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_002_AC4_ATighteningAndALooseningAreBothAuditedAsync()
    {
        await ChangedAsync(Settings.SessionAal2Inactivity, TimeSpan.FromMinutes(30), null, Wanting);
        await ChangedAsync(Settings.SessionAal2Inactivity, TimeSpan.FromHours(2), "a support window", Satisfied);

        Assert.Equal([false, true], _changes.Written.ConvertAll(change => change.Loosening));
    }

    /// <summary>
    /// OPS-CFG-005 AC1: the record carries who, what, from, to, when and why, with the
    /// values written as the settings table writes them.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_005_AC1_TheRecordCarriesBeforeAndAfterAsync()
    {
        var actor = SubjectId.New(_randomness);

        await ChangedAsync(
            Settings.SessionAal2Inactivity,
            TimeSpan.FromHours(2),
            "a support window",
            Satisfied,
            actor);

        ConfigurationChange written = Assert.Single(_changes.Written);

        Assert.Equal(Settings.SessionAal2Inactivity.Key, written.Key);
        Assert.Equal(Settings.SessionAal2Inactivity.Write(Settings.SessionAal2Inactivity.Default), written.Before);
        Assert.Equal(Settings.SessionAal2Inactivity.Write(TimeSpan.FromHours(2)), written.After);
        Assert.Equal("a support window", written.Reason);
        Assert.Equal(actor, written.Actor);
        Assert.Equal(Noon, written.At);
    }

    /// <summary>
    /// OPS-CFG-005 AC2: the records read back by setting and by actor.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_005_AC2_TheRecordsAreQueryableBySettingAndByActorAsync()
    {
        var one = SubjectId.New(_randomness);
        var other = SubjectId.New(_randomness);

        await ChangedAsync(Settings.SessionAal2Inactivity, TimeSpan.FromMinutes(30), null, Wanting, one);
        await ChangedAsync(Settings.SessionAal2Absolute, TimeSpan.FromHours(12), null, Wanting, other);

        Assert.Equal(
            [Settings.SessionAal2Inactivity.Key],
            await KeysAsync(_changes.OfSettingAsync(Settings.SessionAal2Inactivity.Key, TestContext.Current.CancellationToken)));

        Assert.Equal(
            [Settings.SessionAal2Absolute.Key],
            await KeysAsync(_changes.OfActorAsync(other, TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// OPS-CFG-002 and chapter 10 section 2.1: a loosening is the administrator's, so a
    /// caller without <c>system:administer</c> is refused it whatever the session
    /// proved, and nothing is written.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_002_ALooseningIsRefusedWithoutSystemAdministerAsync()
    {
        Result changed = await Administration.ChangeAsync(
            Settings.SessionAal2Inactivity,
            TimeSpan.FromHours(6),
            "a support window",
            Satisfied,
            AccessContext.Of(SubjectId.New(_randomness)),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.Denied,
            changed.Match(() => throw new Xunit.Sdk.XunitException("The change was not refused."), error => error).Code);

        Assert.Empty(_changes.Written);
        Assert.Equal(Settings.SessionAal2Inactivity.Default, await InForceAsync(Settings.SessionAal2Inactivity));
    }

    /// <summary>
    /// OPS-CFG-002 AC1 and chapter 10 section 2.1: a tightening costs nothing, the
    /// permission to loosen included.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_002_ATighteningNeedsNoSystemAdministerAsync()
    {
        Result changed = await Administration.ChangeAsync(
            Settings.SessionAal2Inactivity,
            TimeSpan.FromMinutes(30),
            reason: null,
            Wanting,
            AccessContext.Of(SubjectId.New(_randomness)),
            TestContext.Current.CancellationToken);

        changed.Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The change was refused: {error.Code}."));

        Assert.False(Assert.Single(_changes.Written).Loosening);
    }

    /// <summary>
    /// OPS-CFG-005: background work changes no setting, because a record of a change
    /// nobody made answers for nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_005_ASystemPrincipalChangesNoSettingAsync()
    {
        Result changed = await Administration.ChangeAsync(
            Settings.SessionAal2Inactivity,
            TimeSpan.FromMinutes(30),
            "a sweep",
            Satisfied,
            AccessContext.Of(SystemPrincipal.ForDeployment(
                "sweeper",
                "the nightly sweep",
                SystemOperation.ExpirySweep)),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.Denied,
            changed.Match(() => throw new Xunit.Sdk.XunitException("The change was not refused."), error => error).Code);

        Assert.Empty(_changes.Written);
    }

    /// <summary>
    /// AUTH-FACT-017 AC1 and AC4: raising the system policy's assurance floor records
    /// the raise a sign-in that does not meet it is held against, and lowering the floor
    /// again clears it, so no hold outlives the requirement.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_017_AChangeToTheSystemPolicyRecordsWhatItRaisedAsync()
    {
        Policy raised = Janus.Core.Policies.SystemDefault with { RequiredAssurance = AssuranceLevel.Aal2 };

        await ChangedAsync(Settings.PolicyDefault, raised, "a phishing campaign", Satisfied);

        PolicyRaise raise = Assert.Single(await _raises.OfAsync(null, TestContext.Current.CancellationToken));

        Assert.Equal(PolicyField.RequiredAssurance, raise.Field);
        Assert.Equal(Noon, raise.At);

        await ChangedAsync(Settings.PolicyDefault, Janus.Core.Policies.SystemDefault, "the campaign ended", Satisfied);

        Assert.Empty(await _raises.OfAsync(null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// OPS-ALERT-001 AC1, D-083: a system policy that asks less at a step-up gate raises
    /// the High alert as the change is made, naming the policy and the gate.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_ALERT_001_AC1_ASystemPolicyAskingLessAtAGateRaisesTheAlertAsync()
    {
        Gate enrolling = Janus.Core.Policies.SystemDefault.Gates[StepUpAction.FactorEnrol];

        await ChangedAsync(
            Settings.PolicyDefault,
            Gated(StepUpAction.FactorEnrol, enrolling with { MaximumAge = enrolling.MaximumAge * 4 }),
            "a support window",
            Satisfied);

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.StepUpPolicyWeakened, raised.Condition);
        Assert.Equal(AlertSeverity.High, raised.Severity);
        Assert.Equal("policy.default", raised.Details["key"].GetString());
        Assert.Equal(["factor:enrol"], raised.Details["gates"].EnumerateArray().Select(gate => gate.GetString()));
    }

    /// <summary>
    /// OPS-ALERT-001 AC1, D-083: a gate asked more of weakens nothing and raises nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_ALERT_001_AC1_ASystemPolicyAskingMoreAtAGateRaisesNothingAsync()
    {
        Gate enrolling = Janus.Core.Policies.SystemDefault.Gates[StepUpAction.FactorEnrol];

        await ChangedAsync(
            Settings.PolicyDefault,
            Gated(StepUpAction.FactorEnrol, enrolling with { PhishingResistant = true }),
            reason: null,
            Wanting);

        Assert.Empty(_events.Of<AlertRaised>());
        Assert.Single(_changes.Written);
    }

    /// <summary>
    /// OPS-CFG-002 AC1: the system policy is classified field by field as chapter 10
    /// section 4.1a writes it, so a gate asked more of is free and one asked less of
    /// costs the gate.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_002_AC1_TheSystemPolicyIsClassifiedFieldByFieldAsync()
    {
        Gate enrolling = Janus.Core.Policies.SystemDefault.Gates[StepUpAction.FactorEnrol];

        await ChangedAsync(
            Settings.PolicyDefault,
            Gated(StepUpAction.FactorEnrol, enrolling with { MaximumAge = enrolling.MaximumAge / 2 }),
            reason: null,
            Wanting);

        Assert.Equal(
            ErrorCodes.StepUpRequired,
            (await RefusedAsync(
                Settings.PolicyDefault,
                Janus.Core.Policies.SystemDefault,
                "a support window",
                Wanting)).Code);
    }

    /// <summary>
    /// OPS-CFG-005 and OPS-CFG-008: a member of a family is put in force and written
    /// down with the key, what it was, what it became, the direction its route judged,
    /// the reason and the actor.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_005_AChangedMemberOfAFamilyIsWrittenDownAsync()
    {
        var organization = OrganizationId.New(_clock);
        var actor = SubjectId.New(_randomness);
        PolicyOverride withdrawn = PolicyOverride.None with { SelfServiceRecovery = false };

        Result<PolicyOverride> before = await Administration.ChangeMemberAsync(
            Settings.OrganizationPolicy,
            organization.ToString(),
            withdrawn,
            loosening: false,
            "no recovery by mail",
            actor,
            TestContext.Current.CancellationToken);

        ConfigurationChange written = Assert.Single(_changes.Written);

        Assert.Equal(PolicyOverride.None, before.Match<PolicyOverride?>(value => value, _ => null));
        Assert.Equal(Settings.OrganizationPolicy.For(organization.ToString()), written.Key);
        Assert.Equal(Settings.OrganizationPolicy.Write(PolicyOverride.None), written.Before);
        Assert.Equal(Settings.OrganizationPolicy.Write(withdrawn), written.After);
        Assert.False(written.Loosening);
        Assert.Equal("no recovery by mail", written.Reason);
        Assert.Equal(actor, written.Actor);
        Assert.False(
            (await _configuration.ReadAsync(
                Settings.OrganizationPolicy,
                organization.ToString(),
                TestContext.Current.CancellationToken)).Match(value => value.SelfServiceRecovery, _ => null));
    }

    /// <summary>
    /// OPS-CFG-004: a member of a family the application cannot change is refused
    /// whatever its route judged, and nothing is written down.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_004_AMemberOfAProtectedFamilyIsRefusedAsync()
    {
        Result<bool> refused = await Administration.ChangeMemberAsync(
            Settings.OrganizationStepUpEnforcement,
            OrganizationId.New(_clock).ToString(),
            false,
            loosening: true,
            "an outage",
            SubjectId.New(_randomness),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.ConfigurationKeyProtected,
            refused.Match(_ => throw new Xunit.Sdk.XunitException("The change was not refused."), error => error.Code));
        Assert.Empty(_changes.Written);
    }

    private static async Task<IReadOnlyList<ConfigurationKey>> KeysAsync(
        ValueTask<IReadOnlyList<ConfigurationChange>> reading)
    {
        IReadOnlyList<ConfigurationChange> changes = await reading;
        var keys = new List<ConfigurationKey>(changes.Count);

        foreach (ConfigurationChange change in changes)
        {
            keys.Add(change.Key);
        }

        return keys;
    }

    /// <summary>
    /// INT-MAIL-011 AC2: moving the sending domain to one not declared as registered
    /// with the relay, while Continue with Apple is a way in, raises the warning the
    /// start raises, naming the domain, and the change is made.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_011_AC2_ChangingTheSendingDomainToAnUndeclaredOneWarnsAsync()
    {
        await ChangedAsync(Settings.NotificationEmailSendingDomain, "news.example.test", "a separate stream", Satisfied);

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.RelayDomainUnregistered, raised.Condition);
        Assert.Equal(AlertSeverity.Normal, raised.Severity);
        Assert.Equal("news.example.test", raised.Details["domain"].GetString());
        Assert.Equal("news.example.test", await InForceAsync(Settings.NotificationEmailSendingDomain));
        Assert.Single(_changes.Written);
    }

    /// <summary>
    /// INT-MAIL-011 AC2 (entry 269): withdrawing the declaration, or making Continue
    /// with Apple a way in again over a domain not declared, warns as well.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_011_AC2_EveryChangeThatLeavesTheDomainUndeclaredWarnsAsync()
    {
        Policy withoutApple = Janus.Core.Policies.SystemDefault with
        {
            LoginFactors = Janus.Core.Policies.SystemDefault.LoginFactors.Where(factor => factor is not Factor.Apple).ToHashSet(),
        };

        await ChangedAsync(Settings.PolicyDefault, withoutApple, "no Apple sign-in", Satisfied);
        await ChangedAsync<IReadOnlySet<string>>(
            Settings.NotificationEmailRelayRegistered,
            new HashSet<string>(StringComparer.Ordinal),
            "the registration lapsed",
            Satisfied);

        Assert.Empty(_events.Published);

        await ChangedAsync(Settings.PolicyDefault, Janus.Core.Policies.SystemDefault, "Apple sign-in again", Satisfied);

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.RelayDomainUnregistered, raised.Condition);
        Assert.Equal("mail.example.test", raised.Details["domain"].GetString());
    }

    /// <summary>
    /// INT-MAIL-011 AC3: the declaration is configuration: declaring the new domain as
    /// registered, the way any runtime setting changes, is what quiets the warning.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_011_AC3_DeclaringTheDomainIsAConfigurationChangeAsync()
    {
        await ChangedAsync<IReadOnlySet<string>>(
            Settings.NotificationEmailRelayRegistered,
            new HashSet<string>(["mail.example.test", "News.Example.Test"], StringComparer.Ordinal),
            "the second stream is registered",
            Satisfied);
        await ChangedAsync(Settings.NotificationEmailSendingDomain, "news.example.test", "a separate stream", Satisfied);

        Assert.Empty(_events.Published);
        Assert.Equal(2, _changes.Written.Count);
    }

    /// <summary>
    /// A warning no consumer took leaves the change unmade and unwritten, as any
    /// publication inside a transaction does (entry 269).
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task ChangeAsync_AWarningThatIsNotTaken_IsRefusedAndNotWrittenDownAsync()
    {
        _events.Refusal = Error.From(ErrorCodes.Denied);

        Error refusal = await RefusedAsync(
            Settings.NotificationEmailSendingDomain,
            "news.example.test",
            "a separate stream",
            Satisfied);

        Assert.Equal(ErrorCodes.Denied, refusal.Code);
        Assert.Empty(_changes.Written);
        Assert.Equal(0, _work.Committed);
    }

    private async Task<TValue> InForceAsync<TValue>(Setting<TValue> setting) =>
        (await _configuration.ReadAsync(setting, TestContext.Current.CancellationToken)).Match(
            value => value,
            error => throw new Xunit.Sdk.XunitException($"The setting was refused: {error.Code}."));

    private async Task ChangedAsync<TValue>(
        Setting<TValue> setting,
        TValue value,
        string? reason,
        StepUpChallenge challenge,
        SubjectId? actor = null) =>
        (await Administration.ChangeAsync(
            setting,
            value,
            reason,
            challenge,
            AccessContext.Of(Administrator(actor)),
            TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The change was refused: {error.Code}."));

    private async Task<Error> RefusedAsync<TValue>(
        Setting<TValue> setting,
        TValue value,
        string? reason,
        StepUpChallenge challenge) =>
        (await Administration.ChangeAsync(
            setting,
            value,
            reason,
            challenge,
            AccessContext.Of(Administrator(actor: null)),
            TestContext.Current.CancellationToken)).Match(
            () => throw new Xunit.Sdk.XunitException("The change was not refused."),
            error => error);

    // Whoever a test has make a change holds the permission to loosen, so that what
    // the test is about is what decides.
    private SubjectId Administrator(SubjectId? actor)
    {
        SubjectId administrator = actor ?? SubjectId.New(_randomness);

        _gate.Grant(administrator, _administering, Permissions.SystemAdminister);

        return administrator;
    }
}
