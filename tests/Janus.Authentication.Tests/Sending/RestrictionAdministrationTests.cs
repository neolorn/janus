using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Configuration;
using Janus.Authentication.Tests.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// Editing the named restrictions and granting credit under one: what the gate
/// requires, what applies without a restart, what is written down, and what raises
/// an alert (AUTH-ABUSE-004, OPS-ALERT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class RestrictionAdministrationTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly PhoneNumber Phone = Number("+201001234567");

    private static readonly StepUpChallenge Satisfied =
        new(StepUpOutcome.Satisfied, AssuranceLevel.Aal2, PhishingResistant: false, [], null);

    private static readonly StepUpChallenge Wanting =
        new(StepUpOutcome.Present, AssuranceLevel.Aal2, PhishingResistant: false, [[Factor.Totp]], null);

    private readonly ConfigurationInMemory _configuration = new();
    private readonly SendLedgerInMemory _ledger = new();
    private readonly SendAuditInMemory _audit = new();
    private readonly ConfigurationAuditInMemory _changes = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();

    /// <summary>
    /// A deployment that has named the one key with no default, administered by
    /// whoever edits it here.
    /// </summary>
    public RestrictionAdministrationTests()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);

        var administrative = OrganizationId.New(_clock);
        _administrative.Organization = administrative;
        _gate.GrantEveryone(administrative, Permissions.SystemAdminister);
    }

    private RestrictionAdministration Administration =>
        new(
            _configuration,
            new ConfigurationAdministration(
                _configuration,
                _changes,
                new AdministrativeScope(_gate, _administrative),
                new PolicyResolution(new MembershipLookupInMemory(), _configuration, new PolicyRaiseStoreInMemory()),
                new RelayRegistration(_configuration, _events, _clock),
                _work,
                _clock),
            _ledger,
            _audit,
            RestrictionKeySuppliers.None,
            _work,
            _events,
            _events,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC3: an edit the <c>restriction:edit</c> gate has not answered
    /// is refused with the step-up code, and nothing is written.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC3_AnEditWithoutStepUpIsRefusedAsync()
    {
        Result refused = await Administration.EditAsync(
            "sms.destination",
            Tightened(),
            "an incident",
            Wanting,
            SubjectId.New(_randomness),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.StepUpRequired, Refusal(refused));
        Assert.Empty(_audit.Edits);
        Assert.Empty(_events.Published);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC3: an edit is written down and announced, carrying whether
    /// it let more through than before.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC3_AnEditIsAuditedAndAnnouncedAsync()
    {
        var actor = SubjectId.New(_randomness);

        await EditedAsync("sms.destination", Tightened(), reason: null, actor);

        SendAuditInMemory.Edit written = Assert.Single(_audit.Edits);

        Assert.Equal("sms.destination", written.Name);
        Assert.False(written.Loosening);
        Assert.Null(written.Reason);
        Assert.Equal(actor, written.Actor);

        SendingRestrictionChanged announced = Assert.Single(_events.Of<SendingRestrictionChanged>());

        Assert.Equal("sms.destination", announced.Restriction);
        Assert.False(announced.Loosening);
        Assert.Equal(actor, announced.Actor);
    }

    /// <summary>
    /// OPS-CFG-008 AC4: an edit to a restriction is a runtime change like any other.
    /// The entry carries what the restriction was and what it became, and a loosening
    /// raises the Normal alert.
    /// </summary>
    [Fact]
    public async Task OPS_CFG_008_AC4_AnEditIsLiveAuditedWithBothValuesAndAlertedAsync()
    {
        Restriction shipped = Settings.Restrictions.Default[0];

        await EditedAsync("sms.destination", Loosened(), "a carrier dropped the codes");

        SendAuditInMemory.Edit written = Assert.Single(_audit.Edits);

        Assert.Equal(shipped.Buckets, written.Before!.Buckets);
        Assert.Equal(Loosened().Buckets, written.After!.Buckets);
        Assert.True(written.Loosening);
        Assert.Equal(AlertCondition.RestrictionLoosened, Assert.Single(_events.Of<AlertRaised>()).Condition);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC3: raising a maximum, shortening an interval, removing a
    /// bucket and deleting a restriction each raise the Normal loosening alert.
    /// </summary>
    [Theory]
    [MemberData(nameof(Loosenings))]
    public async Task AUTH_ABUSE_004_AC3_ALooseningRaisesANormalAlertAsync(
        string name,
        Restriction? replacement)
    {
        await EditedAsync(name, replacement, "an incident");

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.RestrictionLoosened, raised.Condition);
        Assert.Equal(AlertSeverity.Normal, raised.Severity);
        Assert.True(Assert.Single(_audit.Edits).Loosening);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC3: a loosening with no written reason is refused, so the
    /// trail never holds one without its why.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC3_ALooseningWithoutAReasonIsRefusedAsync()
    {
        Result refused = await Administration.EditAsync(
            "sms.destination",
            Loosened(),
            reason: null,
            Satisfied,
            SubjectId.New(_randomness),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionReasonRequired, Refusal(refused));
        Assert.Empty(_audit.Edits);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC4: a grant with no written reason is refused.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC4_AGrantWithoutAReasonIsRefusedAsync()
    {
        Result refused = await Administration.GrantAsync(
            "sms.destination",
            Phone.Value,
            2,
            reason: null,
            Satisfied,
            SubjectId.New(_randomness),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionReasonRequired, Refusal(refused));
        Assert.Empty(_audit.Grants);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC4: a grant adds credit to the named key, is written down and
    /// is announced, and neither the record nor the event carries the key itself.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC4_AGrantAddsCreditAndIsAuditedAndAnnouncedAsync()
    {
        var actor = SubjectId.New(_randomness);

        _ledger.Given(new RestrictionKey("sms.destination", Phone.Value), Noon, Noon, Noon);

        (await Administration.GrantAsync(
            "sms.destination",
            Phone.Value,
            2,
            "support: their carrier dropped both",
            Satisfied,
            actor,
            TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The grant was refused: {error.Code}."));

        Assert.Equal(
            ("sms.destination", 2, "support: their carrier dropped both", actor),
            Assert.Single(_audit.Grants));

        SendingRestrictionGranted announced = Assert.Single(_events.Of<SendingRestrictionGranted>());

        Assert.Equal("sms.destination", announced.Restriction);
        Assert.Equal(2, announced.Credit);
        Assert.DoesNotContain(Phone.Value, announced.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(Phone.Value, announced.IdempotencyKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC4: a grant naming a restriction the deployment does not
    /// declare, or no credit at all, is refused.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC4_AGrantOnNothingIsRefusedAsync()
    {
        Assert.Equal(
            ErrorCodes.ConfigurationValueNotAllowed,
            Refusal(await Administration.GrantAsync(
                "no.such.restriction",
                Phone.Value,
                2,
                "a reason",
                Satisfied,
                SubjectId.New(_randomness),
                TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.ConfigurationValueNotAllowed,
            Refusal(await Administration.GrantAsync(
                "sms.destination",
                Phone.Value,
                0,
                "a reason",
                Satisfied,
                SubjectId.New(_randomness),
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// OPS-ALERT-001 AC1: credit added by support raises the Normal grant alert with
    /// no one watching for it.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_001_AC1_AGrantRaisesTheRestrictionGrantedAlertAsync()
    {
        (await Administration.GrantAsync(
            "sms.destination",
            Phone.Value,
            1,
            "a reason",
            Satisfied,
            SubjectId.New(_randomness),
            TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The grant was refused: {error.Code}."));

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.RestrictionGranted, raised.Condition);
        Assert.Equal(AlertSeverity.Normal, raised.Severity);
    }

    /// <summary>
    /// Every restriction in force is readable, the shipped defaults included, which
    /// is what the management application lists.
    /// </summary>
    [Fact]
    public async Task AllAsync_ADeploymentThatEditedNothing_AnswersTheShippedSetAsync()
    {
        IReadOnlyList<Restriction> declared = (await Administration
            .AllAsync(TestContext.Current.CancellationToken)).Match(
            value => value,
            error => throw new Xunit.Sdk.XunitException($"The set was refused: {error.Code}."));

        Assert.Equal(
            ["email.destination", "notification.destination", "sms.destination", "sms.source"],
            declared.Select(one => one.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The cases chapter 02 names as loosenings: a higher maximum, a shorter
    /// interval, a removed bucket and a deleted restriction.
    /// </summary>
    /// <returns>The restriction edited and what it becomes.</returns>
    public static TheoryData<string, Restriction?> Loosenings() =>
        new()
        {
            { "sms.destination", Loosened() },
            {
                "sms.destination",
                new Restriction(
                    "sms.destination",
                    RestrictionKeyKind.Destination,
                    null,
                    RestrictionPurpose.Any,
                    [new Bucket(3, TimeSpan.FromHours(12), BucketWindow.Sliding)])
            },
            {
                "email.destination",
                new Restriction(
                    "email.destination",
                    RestrictionKeyKind.Destination,
                    null,
                    RestrictionPurpose.Any,
                    [new Bucket(5, TimeSpan.FromHours(1), BucketWindow.Sliding)])
            },
            { "sms.destination", null },
        };

    private static Restriction Loosened() =>
        new(
            "sms.destination",
            RestrictionKeyKind.Destination,
            null,
            RestrictionPurpose.Any,
            [new Bucket(10, TimeSpan.FromHours(24), BucketWindow.Sliding)]);

    private static Restriction Tightened() =>
        new(
            "sms.destination",
            RestrictionKeyKind.Destination,
            null,
            RestrictionPurpose.Any,
            [new Bucket(1, TimeSpan.FromHours(24), BucketWindow.Sliding)]);

    private static PhoneNumber Number(string entered) =>
        PhoneNumber.TryParse(entered, out PhoneNumber number)
            ? number
            : throw new Xunit.Sdk.XunitException("The number does not parse.");

    private static SendRequest Texted() =>
        new(
            SendDestination.Of(Phone),
            MessageKind.VerificationCode,
            RestrictionPurpose.Verification,
            "198.51.100.7",
            "en");

    private static ErrorCode Refusal(Result result) =>
        result.Match(
            () => throw new Xunit.Sdk.XunitException("The change was not refused."),
            error => error.Code);

    private static ErrorCode Refusal<TValue>(Result<TValue> result) =>
        result.Match(
            _ => throw new Xunit.Sdk.XunitException("The send was not refused."),
            error => error.Code);

    private async Task EditedAsync(
        string name,
        Restriction? replacement,
        string? reason,
        SubjectId? actor = null)
    {
        Result edited = await Administration.EditAsync(
            name,
            replacement,
            reason,
            Satisfied,
            actor ?? SubjectId.New(_randomness),
            TestContext.Current.CancellationToken);

        edited.Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The edit was refused: {error.Code}."));
    }
}
