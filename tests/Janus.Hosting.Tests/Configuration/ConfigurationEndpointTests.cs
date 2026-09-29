using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Configuration;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Configuration;

/// <summary>
/// Reading and changing one runtime key over <c>GET|PUT /admin/config/{key}</c> of
/// chapter 09 section 8: what each direction costs, what the value has to be, and the
/// keys the route does not change (OPS-CFG-002 to OPS-CFG-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class ConfigurationEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Owned =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly string[] Operations = ["ops@example.test"];

    private static readonly string[] OneLanguage = ["en"];

    private static readonly string[] Elsewhere = ["elsewhere@example.test"];

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public ConfigurationEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// Chapter 09 section 8 (D-153) and OPS-CFG-004: a key reads with its value, its
    /// default, whether the application may change it and which way it loosens.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_004_AKeyReadsWithItsDefaultAndWhetherItIsProtectedAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.ConfigurationRead);

        Answer read = await administrator.SendAsync("GET", "/admin/config/abuse.throttle.enabled");

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("abuse.throttle.enabled", read.Text("key"));
        Assert.True(read.Json().GetProperty("value").GetBoolean());
        Assert.True(read.Json().GetProperty("default").GetBoolean());
        Assert.True(read.Json().GetProperty("protected").GetBoolean());
        Assert.Equal("decrease", read.Text("direction"));
    }

    /// <summary>
    /// A duration reads as the chapter writes it, and a changed value reads back as
    /// the value in force beside the unchanged default.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_008_AChangedKeyReadsBackBesideItsDefaultAsync()
    {
        Browser administrator = await AuthorisedAsync(
            Permissions.ConfigurationRead,
            Permissions.ConfigurationManage);

        Assert.Equal(StatusCodes.Status204NoContent, (await TightenedAsync(administrator)).Status);

        Answer read = await administrator.SendAsync("GET", "/admin/config/session.aal2.inactivity");

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("PT30M", read.Text("value"));
        Assert.Equal("PT1H", read.Text("default"));
        Assert.False(read.Json().GetProperty("protected").GetBoolean());
        Assert.Equal("increase", read.Text("direction"));
    }

    /// <summary>
    /// AUTHZ-SCOPE-001: reading the configuration is the administrative organization's
    /// <c>config:read</c>, and a caller without it is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_AReadWithoutConfigReadIsRefusedAsync()
    {
        Browser caller = await AuthorisedAsync(Permissions.ConfigurationManage);

        Answer read = await caller.SendAsync("GET", "/admin/config/session.aal2.inactivity");

        Assert.Equal(StatusCodes.Status403Forbidden, read.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), read.Text("code"));
    }

    /// <summary>
    /// OPS-CFG-002 AC1: a tightening over the endpoint needs neither step-up nor the
    /// permission to loosen, and is written down with its reason.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_002_AC1_ATighteningTakesEffectWithoutStepUpAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.ConfigurationManage);

        // Past the recency any gate asks, so a step-up would not be met.
        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer changed = await TightenedAsync(administrator);

        Assert.Equal(StatusCodes.Status204NoContent, changed.Status);
        Assert.Equal(TimeSpan.FromMinutes(30), await InForceAsync(Settings.SessionAal2Inactivity));

        ConfigurationChange written = Assert.Single(_deployment.Changes.Written);

        Assert.False(written.Loosening);
        Assert.Equal("a shorter window", written.Reason);
    }

    /// <summary>
    /// OPS-CFG-002 AC2: a loosening from a session whose proof is no longer recent is
    /// refused with the step-up code, and nothing changes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_002_AC2_ALooseningRequiresStepUpAsync()
    {
        Browser administrator = await AuthorisedAsync(
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer changed = await LoosenedAsync(administrator);

        Assert.Equal(StatusCodes.Status403Forbidden, changed.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), changed.Text("code"));
        Assert.Equal(Settings.SessionAal2Inactivity.Default, await InForceAsync(Settings.SessionAal2Inactivity));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// OPS-CFG-002 AC2 and AC4: a loosening from a session that has proved itself
    /// recently takes effect and is written down as a loosening with its reason.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_002_AC2_ALooseningWithStepUpAndAReasonTakesEffectAsync()
    {
        Browser administrator = await AuthorisedAsync(
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        Answer changed = await LoosenedAsync(administrator);

        Assert.Equal(StatusCodes.Status204NoContent, changed.Status);
        Assert.Equal(TimeSpan.FromHours(2), await InForceAsync(Settings.SessionAal2Inactivity));

        ConfigurationChange written = Assert.Single(_deployment.Changes.Written);

        Assert.True(written.Loosening);
        Assert.Equal("a support window", written.Reason);
    }

    /// <summary>
    /// OPS-CFG-002 and chapter 10 section 2.1: a loosening is the holder of
    /// <c>system:administer</c>'s, so <c>config:manage</c> alone is refused it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_002_ALooseningRequiresSystemAdministerAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.ConfigurationManage);

        Answer changed = await LoosenedAsync(administrator);

        Assert.Equal(StatusCodes.Status403Forbidden, changed.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), changed.Text("code"));
        Assert.Equal(Settings.SessionAal2Inactivity.Default, await InForceAsync(Settings.SessionAal2Inactivity));
    }

    /// <summary>
    /// OPS-CFG-006 AC1: the owner of an organization, a member of it holding every
    /// permission the library ships there and nothing in the administrative
    /// organization, is refused a loosening, and nothing changes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_006_AC1_AnOrganizationsOwnerWithoutTheGrantChangesNothingAsync()
    {
        Browser owner = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.Organizations.Seed(Owned);
        _deployment.Memberships.Place(subject, Owned);

        foreach (Permission permission in Permissions.All)
        {
            _deployment.Gate.Grant(subject, Owned, permission);
        }

        Answer changed = await LoosenedAsync(owner);

        Assert.Equal(StatusCodes.Status403Forbidden, changed.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), changed.Text("code"));
        Assert.Equal(Settings.SessionAal2Inactivity.Default, await InForceAsync(Settings.SessionAal2Inactivity));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// AUTHZ-SCOPE-001: changing the configuration is the administrative
    /// organization's <c>config:manage</c>, and a caller without it changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_AChangeWithoutConfigManageIsRefusedAsync()
    {
        Browser caller = await AuthorisedAsync(Permissions.ConfigurationRead, Permissions.SystemAdminister);

        Answer changed = await TightenedAsync(caller);

        Assert.Equal(StatusCodes.Status403Forbidden, changed.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), changed.Text("code"));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// OPS-CFG-005 and chapter 09 section 8: every change carries a reason, a
    /// tightening included. One without, or with nothing but spaces, is refused with
    /// the reason code naming the key; one past the 1024 characters of API-CONV-002 is
    /// a request the boundary does not read, naming <c>reason</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_005_EveryChangeCarriesAReasonAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.ConfigurationManage);

        Answer changed = await administrator.SendAsync(
            "PUT",
            "/admin/config/session.aal2.inactivity",
            ("value", "PT30M"));

        Answer blank = await administrator.SendAsync(
            "PUT",
            "/admin/config/session.aal2.inactivity",
            ("value", "PT30M"),
            ("reason", "   "));

        Answer overlong = await administrator.SendAsync(
            "PUT",
            "/admin/config/session.aal2.inactivity",
            ("value", "PT30M"),
            ("reason", new string('r', 1025)));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, changed.Status);
        Assert.Equal(ErrorCodes.ConfigurationChangeReasonRequired.ToString(), changed.Text("code"));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, blank.Status);
        Assert.Equal(ErrorCodes.ConfigurationChangeReasonRequired.ToString(), blank.Text("code"));
        Assert.Equal(
            Settings.SessionAal2Inactivity.Key.ToString(),
            blank.Json().GetProperty("details").GetProperty("key").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, overlong.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), overlong.Text("code"));
        Assert.Equal("reason", overlong.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal(Settings.SessionAal2Inactivity.Default, await InForceAsync(Settings.SessionAal2Inactivity));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// OPS-CFG-003 AC1: a password floor below the standard's minimum is rejected at
    /// the point of change.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_003_AC1_APasswordFloorBelowTheMinimumIsRejectedAsync()
    {
        Browser administrator = await AuthorisedAsync(
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        Answer changed = await administrator.SendAsync(
            "PUT",
            "/admin/config/password.floor.withmfa",
            ("value", 7),
            ("reason", "a shorter password"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, changed.Status);
        Assert.Equal(ErrorCodes.ConfigurationValueBelowFloor.ToString(), changed.Text("code"));
    }

    /// <summary>
    /// OPS-CFG-003 AC2: a session absolute timeout above the maximum is rejected with a
    /// named error, naming the key.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_003_AC2_ASessionTimeoutAboveTheMaximumIsRejectedAsync()
    {
        Browser administrator = await AuthorisedAsync(
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        Answer changed = await administrator.SendAsync(
            "PUT",
            "/admin/config/session.aal2.absolute",
            ("value", "PT25H"),
            ("reason", "a longer day"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, changed.Status);
        Assert.Equal(ErrorCodes.ConfigurationValueAboveCeiling.ToString(), changed.Text("code"));
        Assert.Equal("session.aal2.absolute", changed.Json().GetProperty("details").GetProperty("key").GetString());
    }

    /// <summary>
    /// Chapter 10 section 1.5: a value of the wrong type is not allowed, a number for a
    /// duration and a string for a number alike.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_003_AValueOfTheWrongTypeIsNotAllowedAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.ConfigurationManage);

        Answer duration = await administrator.SendAsync(
            "PUT",
            "/admin/config/session.aal2.inactivity",
            ("value", 30),
            ("reason", "a shorter window"));

        Answer number = await administrator.SendAsync(
            "PUT",
            "/admin/config/password.floor.withmfa",
            ("value", "12"),
            ("reason", "a longer password"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, duration.Status);
        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed.ToString(), duration.Text("code"));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, number.Status);
        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed.ToString(), number.Text("code"));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// OPS-CFG-004 AC1: no runtime API modifies a protected key, whoever asks.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_004_AC1_AProtectedKeyIsRefusedAsync()
    {
        Browser administrator = await AuthorisedAsync(
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        Answer changed = await administrator.SendAsync(
            "PUT",
            "/admin/config/abuse.throttle.enabled",
            ("value", false),
            ("reason", "a load test"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, changed.Status);
        Assert.Equal(ErrorCodes.ConfigurationKeyProtected.ToString(), changed.Text("code"));
        Assert.True(await InForceAsync(Settings.AbuseThrottleEnabled));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// OPS-ALERT-006 AC2: the export audit is a protected key, so no call through the
    /// application turns it off, the administrator's own included.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_006_AC2_ExportAuditingCannotBeDisabledThroughTheApplicationAsync()
    {
        Browser administrator = await AuthorisedAsync(
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        Answer changed = await administrator.SendAsync(
            "PUT",
            "/admin/config/exfiltration.export.auditing",
            ("value", false),
            ("reason", "a quarterly export"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, changed.Status);
        Assert.Equal(ErrorCodes.ConfigurationKeyProtected.ToString(), changed.Text("code"));
        Assert.True(await InForceAsync(Settings.ExfiltrationExportAuditing));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// AUTH-ABUSE-004 and chapter 10 section 2.1: the named restriction set has its
    /// own operations and its own permission, so it is no key of this route.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_TheRestrictionSetIsNoKeyOfTheConfigurationRouteAsync()
    {
        Browser administrator = await AuthorisedAsync(
            Permissions.ConfigurationRead,
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        Answer read = await administrator.SendAsync("GET", "/admin/config/restrictions");
        Answer changed = await administrator.SendAsync(
            "PUT",
            "/admin/config/restrictions",
            ("value", Array.Empty<string>()),
            ("reason", "no limits"));

        Assert.Equal(StatusCodes.Status400BadRequest, read.Status);
        Assert.Equal("key", read.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, changed.Status);
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// LIB-API-005: a name the catalogue does not hold is no key, and the request is
    /// malformed rather than refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_005_ANameOutsideTheCatalogueIsMalformedAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.ConfigurationRead);

        Answer read = await administrator.SendAsync("GET", "/admin/config/no.such.key");

        Assert.Equal(StatusCodes.Status400BadRequest, read.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), read.Text("code"));
        Assert.Equal("key", read.Json().GetProperty("details").GetProperty("member").GetString());
    }

    /// <summary>
    /// OPS-ALERT-004a AC1: the destination keys change through the one way that tells
    /// the destinations being replaced, and the change is written down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_004a_AC1_ADestinationChangeTellsThePreviousDestinationsAsync()
    {
        _deployment.Configuration.Set(Settings.NotificationLanguages, OneLanguage);
        _deployment.Configuration.Set(Settings.AlertingEmailDestinations, Operations);

        Browser administrator = await AuthorisedAsync(
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        int before = _deployment.Mail.Taken.Count;

        Answer changed = await administrator.SendAsync(
            "PUT",
            "/admin/config/alerting.email.destinations",
            ("value", Elsewhere),
            ("reason", "a new rota"));

        Assert.Equal(StatusCodes.Status204NoContent, changed.Status);
        Assert.Contains(
            _deployment.Mail.Taken.Skip(before),
            mail => string.Equals(mail.Destination.Value, Operations[0], StringComparison.Ordinal));
        Assert.Equal(
            Settings.AlertingEmailDestinations.Key,
            Assert.Single(_deployment.Changes.Written).Key);
    }

    /// <summary>
    /// CONV-CODE-006 AC2 and chapter 09 section 8: a change whose body carries no reason
    /// is refused with the reason code, naming the key, before the service is reached,
    /// so a caller the service would refuse for want of the permission is answered for
    /// the body, and the value in force is unchanged.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_006_AC2_ABodyMissingItsReasonIsRefusedBeforeTheServiceAsync()
    {
        Browser caller = await AuthorisedAsync();

        Answer changed = await caller.SendAsync(
            "PUT",
            "/admin/config/session.aal2.inactivity",
            ("value", "PT30M"),
            ("reason", null));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, changed.Status);
        Assert.Equal(ErrorCodes.ConfigurationChangeReasonRequired.ToString(), changed.Text("code"));
        Assert.Equal(
            Settings.SessionAal2Inactivity.Key.ToString(),
            changed.Json().GetProperty("details").GetProperty("key").GetString());
        Assert.Equal(Settings.SessionAal2Inactivity.Default, await InForceAsync(Settings.SessionAal2Inactivity));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// PRIV-RET-001 and chapter 09 section 8: the retention of a category the host
    /// declared reads with its floor as the default where no period is written, and
    /// changes through the route under its row's lock: a lengthening is a tightening, a
    /// shortening is a loosening that asks the permission to loosen, a period below the
    /// floor is refused, and each change is written down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_001_ACategorysRetentionIsChangedThroughTheRouteAsync()
    {
        ConfigurationKey key = Settings.HostCategoryRetention.For("statement");
        string floor = Settings.HostCategoryRetention.Write(TimeSpan.FromDays(1826));

        Browser manager = await AuthorisedAsync(
            Permissions.ConfigurationRead,
            Permissions.ConfigurationManage);

        Answer read = await manager.SendAsync("GET", "/admin/config/retention.statement");

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("retention.statement", read.Text("key"));
        Assert.Equal(floor, read.Text("value"));
        Assert.Equal(floor, read.Text("default"));
        Assert.False(read.Json().GetProperty("protected").GetBoolean());
        Assert.Equal("decrease", read.Text("direction"));

        _deployment.Configuration.Held.Clear();
        _deployment.Work.Reset();

        Answer lengthened = await RetainedAsync(manager, "P2000D");

        Assert.Equal(StatusCodes.Status204NoContent, lengthened.Status);
        Assert.Equal([key, key], _deployment.Configuration.Held);
        Assert.Equal(_deployment.Work.Opened, _deployment.Work.Committed);

        Answer shortened = await RetainedAsync(manager, "P1900D");

        Assert.Equal(StatusCodes.Status403Forbidden, shortened.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), shortened.Text("code"));
        Assert.Equal(_deployment.Work.Opened, _deployment.Work.Committed);

        Answer below = await RetainedAsync(manager, "P1000D");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, below.Status);
        Assert.Equal(ErrorCodes.ConfigurationValueBelowFloor.ToString(), below.Text("code"));
        Assert.Equal(key.ToString(), below.Json().GetProperty("details").GetProperty("key").GetString());
        Assert.Equal(TimeSpan.FromDays(2000), await RetainedForAsync("statement"));

        ConfigurationChange tightening = Assert.Single(_deployment.Changes.Written);

        Assert.Equal(key, tightening.Key);
        Assert.Equal(floor, tightening.Before);
        Assert.Equal("P2000D", tightening.After);
        Assert.False(tightening.Loosening);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.SystemAdminister);

        Answer loosened = await RetainedAsync(manager, "P1900D");

        Assert.Equal(StatusCodes.Status204NoContent, loosened.Status);
        Assert.Equal(TimeSpan.FromDays(1900), await RetainedForAsync("statement"));
        Assert.True(_deployment.Changes.Written[^1].Loosening);
    }

    /// <summary>
    /// PRIV-RET-001 and chapter 09 section 8: a retention key of a category the host
    /// did not declare is none of the keys the route serves, read or changed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_001_AnUndeclaredCategoryIsNoKeyAsync()
    {
        Browser administrator = await AuthorisedAsync(
            Permissions.ConfigurationRead,
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        Answer read = await administrator.SendAsync("GET", "/admin/config/retention.undeclared");
        Answer changed = await RetainedAsync(administrator, "P2000D", "undeclared");

        foreach (Answer answer in new[] { read, changed })
        {
            Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);
            Assert.Equal(ErrorCodes.RequestMalformed.ToString(), answer.Text("code"));
            Assert.Equal("key", answer.Json().GetProperty("details").GetProperty("member").GetString());
        }

        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// OPS-ALERT-001 and OPS-ALERT-006 AC5: turning the export step-up off raises the
    /// High <c>stepup-policy-weakened</c> alert naming the key as the change is made;
    /// turning it back on weakens nothing and raises nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_001_TurningExportStepUpOffRaisesStepUpPolicyWeakenedAsync()
    {
        Browser administrator = await AuthorisedAsync(
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        Answer off = await administrator.SendAsync(
            "PUT",
            "/admin/config/exfiltration.export.stepuprequired",
            ("value", false),
            ("reason", "a supervised migration"));
        Answer on = await administrator.SendAsync(
            "PUT",
            "/admin/config/exfiltration.export.stepuprequired",
            ("value", true),
            ("reason", "the migration ended"));

        AlertRaised raised = Assert.Single(
            _deployment.Events.Of<AlertRaised>(),
            alert => alert.Condition is AlertCondition.StepUpPolicyWeakened);

        Assert.Equal(StatusCodes.Status204NoContent, off.Status);
        Assert.Equal(StatusCodes.Status204NoContent, on.Status);
        Assert.Equal(AlertSeverity.High, raised.Severity);
        Assert.Equal(
            Settings.ExfiltrationExportStepUpRequired.Key.ToString(),
            raised.Details["key"].GetString());
    }

    private static Task<Answer> RetainedAsync(Browser browser, string period, string category = "statement") =>
        browser.SendAsync(
            "PUT",
            "/admin/config/retention." + category,
            ("value", period),
            ("reason", "the statements' audit horizon"));

    private async Task<TimeSpan> RetainedForAsync(string category) =>
        (await _deployment.Configuration.ReadAsync(
            Settings.HostCategoryRetention,
            category,
            TestContext.Current.CancellationToken)).Match(
            value => value,
            error => throw new Xunit.Sdk.XunitException($"The member was refused: {error.Code}."));

    private static Task<Answer> TightenedAsync(Browser browser) =>
        browser.SendAsync(
            "PUT",
            "/admin/config/session.aal2.inactivity",
            ("value", "PT30M"),
            ("reason", "a shorter window"));

    private static Task<Answer> LoosenedAsync(Browser browser) =>
        browser.SendAsync(
            "PUT",
            "/admin/config/session.aal2.inactivity",
            ("value", "PT2H"),
            ("reason", "a support window"));

    private async Task<TValue> InForceAsync<TValue>(Setting<TValue> setting) =>
        (await _deployment.Configuration.ReadAsync(setting, TestContext.Current.CancellationToken)).Match(
            value => value,
            error => throw new Xunit.Sdk.XunitException($"The setting was refused: {error.Code}."));

    private async Task<Browser> AuthorisedAsync(params Permission[] permissions)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        foreach (Permission permission in permissions)
        {
            _deployment.Gate.Grant(subject, Administration, permission);
        }

        return browser;
    }
}
