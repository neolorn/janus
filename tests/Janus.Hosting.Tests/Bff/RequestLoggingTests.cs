using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// What a request leaves in the log of a deployment logging everything: its refusal
/// under the identifier the answer carries, the detail of a fault the answer
/// withheld, and never an address or a number a person typed (BFF-ERR-002,
/// BFF-LOG-001, CONV-LOG-004, INF-OBS-002); and what a security event leaves in a
/// deployment logging nothing (CONV-LOG-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class RequestLoggingTests : IAsyncDisposable
{
    private const string Language = "en";
    private const string Erasures = "/admin/erasures";
    private const string Wrong = "notthepasswordatall";
    private const string Renewed = "lemoncurdandbutter";
    private const string Unheld = "nobody@example.test";
    private const string UnheldNumber = "+441632960077";
    private const string Operator = "ops@example.test";
    private const string OperatorNumber = "+441632960098";
    private const string Owner = "owner@example.test";
    private const string OwnerNumber = "+441632960099";

    // The subscriber a manual completion appends the ledger's line through, which is
    // what the fault names and the answer withholds.
    private const string Ledger = "erasure-ledger";

    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment that can send every message a registration, a sign-in and a
    /// recovery write, the links carrying their token as the shipped templates do.
    /// </summary>
    public RequestLoggingTests()
    {
        Flow.Prepare(_deployment);

        foreach (MessageKind message in new[]
                 {
                     MessageKind.SignInLink,
                     MessageKind.RecoveryLink,
                     MessageKind.SecurityNotice,
                     MessageKind.NoAccount,
                 })
        {
            foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
            {
                _deployment.Templates.Set(
                    message,
                    kind,
                    Language,
                    new MessageTemplate(kind is SendKind.Email ? "subject" : null, "{token}"));
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// BFF-ERR-002 AC2: a fault answers with the correlation identifier alone, and
    /// the code and the context it withheld are in the log under that identifier.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_002_AC2_AFaultsDetailIsRetrievableByItsCorrelationIdentifierAsync()
    {
        Delivery delivery = await FailedAsync();
        Browser browser = await AuthorisedAsync();

        _deployment.Ledger.Durable = false;

        Answer faulted = await browser.SendAsync("POST", $"{Erasures}/{delivery.Id.Value}/complete");

        IReadOnlyList<string> resolved = Resolved(faulted.Text("correlationId"));

        Assert.Equal(StatusCodes.Status500InternalServerError, faulted.Status);
        Assert.Empty(faulted.Json().GetProperty("details").EnumerateObject());
        Assert.DoesNotContain(Ledger, faulted.Body, StringComparison.Ordinal);
        Assert.Contains(
            resolved,
            line => line.Contains(ErrorCodes.SystemFault.ToString(), StringComparison.Ordinal)
                && line.Contains(Ledger, StringComparison.Ordinal));
    }

    /// <summary>
    /// BFF-LOG-001 AC1: the identifier a denial answers with finds the entries that
    /// request wrote, the refusal among them, and no entry another request wrote;
    /// every entry the library wrote while the request ran carries it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_LOG_001_AC1_ADenialsIdentifierResolvesToThatRequestsEntriesAsync()
    {
        _ = await FailedAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);

        int before = _deployment.Logs.Lines.Count;
        Answer denied = await browser.SendAsync("GET", Erasures);
        string[] written = [.. _deployment.Logs.Lines.Skip(before)];

        Answer other = await browser.SendAsync("GET", Erasures);

        string correlation = denied.Text("correlationId");
        IReadOnlyList<string> resolved = Resolved(correlation);

        Assert.Equal(StatusCodes.Status403Forbidden, denied.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), denied.Text("code"));
        Assert.Contains(resolved, line => line.Contains(ErrorCodes.Denied.ToString(), StringComparison.Ordinal));
        Assert.Equal(resolved, [.. written.Where(line => line.Contains(correlation, StringComparison.Ordinal))]);
        Assert.All(
            written.Where(line => line.StartsWith("Janus.", StringComparison.Ordinal)),
            line => Assert.Contains(correlation, line, StringComparison.Ordinal));
        Assert.DoesNotContain(
            Resolved(other.Text("correlationId")),
            line => line.Contains(correlation, StringComparison.Ordinal));
    }

    /// <summary>
    /// CONV-LOG-004 AC1: registration, sign-in and recovery, each carrying an address
    /// and a number, with the deployment logging at every level, leave neither in any
    /// line, nor the address or the number no account holds.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_LOG_004_AC1_NoRequestLogsAnAddressOrANumberAsync()
    {
        await CarriedAsync();

        AssertNothingPersonalLogged();
    }

    /// <summary>
    /// INF-OBS-002 AC2: what the deployment hands its log store for the same flows
    /// holds no address and no number, so the store holds nothing an erasure would
    /// have to reach.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_OBS_002_AC2_DefaultLogOutputHoldsNoAddressOrNumberAsync()
    {
        await CarriedAsync();

        AssertNothingPersonalLogged();
    }

    /// <summary>
    /// CONV-LOG-005 AC1: with the host logging nothing at all, a failed sign-in, a
    /// refused break-glass code, a failed step-up, a step-up, a denied permission, the
    /// use of the break-glass credential and a configuration change are each recorded,
    /// none of it through the log.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_LOG_005_AC1_RaisingTheLogLevelSuppressesNoSecurityEventAsync()
    {
        await using var quiet = new Deployment(logging: LogLevel.None);
        using var randomness = RandomNumberGenerator.Create();
        SubjectId emergency = Administered(quiet, randomness);

        Browser administrator = await Flow.SignedInAsync(quiet);
        SubjectId subject = quiet.Directory.Created[^1].Subject;

        quiet.Gate.Grant(subject, Company, Permissions.SystemAdminister);

        string credential = (await administrator.SendAsync("POST", "/admin/break-glass/generate")).Text("credential");
        Browser stranger = new(quiet);

        _ = await stranger.SendAsync("GET", "/auth/session");

        Answer failed = await stranger.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", await BegunAsync(stranger, Flow.Address)),
            ("factor", "password"),
            ("value", Wrong));

        Answer guessed = await BrokenAsync(new Browser(quiet), BreakGlassCode.Draw(randomness));

        Answer refused = await administrator.SendAsync(
            "POST",
            "/auth/step-up",
            ("challengeId", await BegunAsync(administrator, Flow.Address)),
            ("factor", "password"),
            ("value", Wrong));

        string challenge = await BegunAsync(administrator, Flow.Address);
        int presented = quiet.SessionAudit.Records.Count;

        Answer stepped = await administrator.SendAsync(
            "POST",
            "/auth/step-up",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", Flow.Password));

        (SessionId Session, SubjectId Subject, IReadOnlyCollection<Factor> Presented) raised =
            quiet.SessionAudit.Records[presented];

        Answer denied = await administrator.SendAsync("GET", Erasures);
        var owner = new Browser(quiet);
        Answer used = await BrokenAsync(owner, credential);

        Answer changed = await owner.SendAsync(
            "PUT",
            "/admin/config/alerting.email.destinations",
            ("value", new[] { Operator }),
            ("reason", "The rota changed."));

        Assert.Equal(ErrorCodes.FactorRejected.ToString(), failed.Text("code"));
        Assert.Equal(ErrorCodes.BreakGlassInvalid.ToString(), guessed.Text("code"));
        Assert.Equal(ErrorCodes.FactorRejected.ToString(), refused.Text("code"));
        Assert.Equal("complete", stepped.Text("status"));
        Assert.Equal(StatusCodes.Status403Forbidden, denied.Status);
        Assert.Equal(StatusCodes.Status200OK, used.Status);
        Assert.Equal(StatusCodes.Status204NoContent, changed.Status);

        (SessionId Session, SubjectId Subject, Factor Presented) unraised =
            Assert.Single(quiet.SessionAudit.StepUpsFailed);

        Assert.Equal<(SubjectId?, Factor)>(
            [(subject, Factor.Password), (emergency, Factor.BreakGlass)],
            quiet.SessionAudit.Failed);
        Assert.Equal((subject, Factor.Password), (unraised.Subject, unraised.Presented));
        Assert.Equal(unraised.Session, raised.Session);
        Assert.Equal(subject, raised.Subject);
        Assert.Contains(Factor.Password, raised.Presented);
        Assert.Single(quiet.Gate.Refusals);
        Assert.Equal(emergency, Assert.Single(quiet.BreakGlassAudit.Used).Emergency);
        Assert.Equal(emergency, Assert.Single(quiet.Changes.Written).Actor);
        Assert.Empty(quiet.Logs.Lines);
    }

    // What a deployment logging everything wrote for the flows: something, the refused
    // sign-in among it, and nothing a person typed to identify themselves. The number
    // is looked for without its country code as well, whatever form it was written in.
    private void AssertNothingPersonalLogged()
    {
        string[] lines = [.. _deployment.Logs.Lines];

        Assert.Contains(lines, line => line.Contains(ErrorCodes.FactorRejected.ToString(), StringComparison.Ordinal));

        foreach (string typed in new[]
                 {
                     Flow.Address,
                     Flow.Number,
                     Flow.Number[3..],
                     Unheld,
                     UnheldNumber,
                     UnheldNumber[3..],
                 })
        {
            Assert.DoesNotContain(lines, line => line.Contains(typed, StringComparison.OrdinalIgnoreCase));
        }
    }

    // Registration with an address and a number, a sign-in opened with each and with
    // one no account holds, a refused password, a held sign-in completed by its code, a
    // link asked for, and a recovery asked for by each and completed by its link.
    private async Task CarriedAsync()
    {
        _ = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        Browser signing = await ArrivedAsync();

        Answer refused = await signing.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", await BegunAsync(signing, Flow.Address)),
            ("factor", "password"),
            ("value", Wrong));

        string challenge = await BegunAsync(signing, Flow.Address);

        Answer held = await signing.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", Flow.Password));

        Answer completed = await signing.SendAsync(
            "POST",
            "/auth/device/verify",
            ("challengeId", challenge),
            ("code", Flow.Code(_deployment, IdentifierKind.Email)));

        _ = await BegunAsync(signing, Flow.Number);
        _ = await BegunAsync(signing, Unheld);
        _ = await BegunAsync(signing, UnheldNumber);

        Answer linked = await signing.SendAsync("POST", "/auth/link", ("identifier", Flow.Address));

        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        Browser recovering = await ArrivedAsync();

        foreach (string identifier in new[] { Unheld, UnheldNumber, Flow.Number, Flow.Address })
        {
            Assert.Equal(
                StatusCodes.Status202Accepted,
                (await recovering.SendAsync("POST", "/recovery/begin", ("identifier", identifier))).Status);
        }

        Answer recovered = await recovering.SendAsync(
            "POST",
            "/recovery/complete",
            ("token", _deployment.Mail.Taken[^1].Body.Trim()),
            ("password", Renewed));

        Assert.Equal(ErrorCodes.FactorRejected.ToString(), refused.Text("code"));
        Assert.Equal("deviceVerificationRequired", held.Text("status"));
        Assert.Equal("complete", completed.Text("status"));
        Assert.Equal(StatusCodes.Status202Accepted, linked.Status);
        Assert.Equal(StatusCodes.Status204NoContent, recovered.Status);
    }

    // Every line the deployment logged that carries the identifier.
    private IReadOnlyList<string> Resolved(string correlation)
    {
        Assert.NotEqual(string.Empty, correlation);

        return [.. _deployment.Logs.Lines.Where(line => line.Contains(correlation, StringComparison.Ordinal))];
    }

    // A browser that has been to the deployment once, which leaves it holding the
    // first contact every state change presents back (BFF-CSRF-005a).
    private async Task<Browser> ArrivedAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        return browser;
    }

    // A deployment administered by one organization, whose reserved account holds every
    // permission there and whose operator the alerts a break-glass use raises reach.
    private static SubjectId Administered(Deployment deployment, RandomNumberGenerator randomness)
    {
        Flow.Prepare(deployment);
        deployment.Administers(Company);

        deployment.Configuration.Set(Settings.AlertingEmailDestinations, [Operator]);
        deployment.Configuration.Set(Settings.AlertingSmsDestinations, [OperatorNumber]);
        deployment.Configuration.Set(Settings.AlertingOwnerEmail, Owner);
        deployment.Configuration.Set(Settings.AlertingOwnerSms, OwnerNumber);
        deployment.Configuration.Set(Settings.AlertingOwnerEnabled, false);

        var emergency = SubjectId.New(randomness);

        deployment.Reserves(emergency);
        deployment.Accounts.Stands(emergency, AccountState.Active);

        foreach (Permission permission in Permissions.All)
        {
            deployment.Gate.Grant(emergency, Company, permission);
        }

        return emergency;
    }

    // The break-glass credential presented as it was typed.
    private static Task<Answer> BrokenAsync(Browser browser, string credential) =>
        browser.SendAsync("POST", "/auth/break-glass", JsonSerializer.Serialize(new { credential }));

    private static async Task<string> BegunAsync(Browser browser, string identifier)
    {
        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", identifier));

        Assert.Equal(StatusCodes.Status200OK, began.Status);

        return began.Text("challengeId");
    }

    // A signed-in browser whose account may manage privacy requests in the
    // administrative organization, which is what the erasure endpoints ask.
    private async Task<Browser> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.Administers(Company);
        _deployment.Gate.Grant(subject, Company, Permissions.PrivacyRequestManage);

        return browser;
    }

    // An erasure whose retries were spent: its delivery and its row, failed together.
    private async Task<Delivery> FailedAsync()
    {
        DateTimeOffset at = _deployment.Clock.GetUtcNow();
        var delivery = Delivery.Of(Ahmed, SubjectEventKind.ErasureRequested, at, reason: ErasureReason.MinorTakedown);
        var row = Erasure.Begun(Ahmed, at, ErasureReason.MinorTakedown);

        delivery.Fail();
        row.Fail();

        await _deployment.Outbox.AddAsync(delivery, TestContext.Current.CancellationToken);
        _deployment.Erasures.Add(row);

        return delivery;
    }
}
