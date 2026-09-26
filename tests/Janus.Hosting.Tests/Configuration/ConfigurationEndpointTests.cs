using System;
using System.Linq;
using System.Threading.Tasks;
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
        Assert.Equal(nameof(SettingDirection.Decrease), read.Text("direction"));
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
        Assert.Equal(nameof(SettingDirection.Increase), read.Text("direction"));
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
    /// tightening included, and one without is refused with the reason code.
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

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, changed.Status);
        Assert.Equal(ErrorCodes.RestrictionReasonRequired.ToString(), changed.Text("code"));
        Assert.Equal(Settings.SessionAal2Inactivity.Default, await InForceAsync(Settings.SessionAal2Inactivity));
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
