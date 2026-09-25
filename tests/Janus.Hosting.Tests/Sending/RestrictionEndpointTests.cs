using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Sending;

/// <summary>
/// The named restriction set over <c>/admin/restrictions</c> of chapter 09 section 8:
/// reading it, editing and deleting one restriction behind <c>restriction:edit</c> and
/// its step-up, and granting credit behind <c>restriction:grant</c> (AUTH-ABUSE-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class RestrictionEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly object[] Tighter =
        [new { max = 2, interval = "PT24H", window = "sliding" }];

    private static readonly object[] Looser =
        [new { max = 10, interval = "PT24H", window = "sliding" }];

    private static readonly object[] Unwindowed =
        [new { max = 2, interval = "PT24H", window = "tumbling" }];

    private static readonly object[] Nothing = [];

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public RestrictionEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// AUTH-ABUSE-004 and chapter 09 section 8: the set reads with its keys, purposes
    /// and buckets, the shipped defaults included.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_TheSetReadsWithTheShippedDefaultsAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit);

        Answer read = await administrator.SendAsync("GET", "/admin/restrictions/");

        Assert.Equal(StatusCodes.Status200OK, read.Status);

        JsonElement email = read.Json()
            .EnumerateArray()
            .Single(one => one.GetProperty("name").GetString() == "email.destination");

        Assert.Equal("destination", email.GetProperty("key").GetString());
        Assert.Equal("any", email.GetProperty("purpose").GetString());

        JsonElement fixedBucket = email.GetProperty("buckets")[1];

        Assert.Equal(1, fixedBucket.GetProperty("max").GetInt32());
        Assert.Equal("PT1M", fixedBucket.GetProperty("interval").GetString());
        Assert.Equal("fixed", fixedBucket.GetProperty("window").GetString());
    }

    /// <summary>
    /// AUTH-ABUSE-004: one restriction reads by its name, and a name no restriction has
    /// is a malformed request naming <c>name</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_OneRestrictionReadsByItsNameAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit);

        Answer read = await administrator.SendAsync("GET", "/admin/restrictions/notification.destination");
        Answer unknown = await administrator.SendAsync("GET", "/admin/restrictions/no.such.restriction");

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("notification", read.Text("purpose"));
        Assert.Equal(StatusCodes.Status400BadRequest, unknown.Status);
        Assert.Equal("name", unknown.Json().GetProperty("details").GetProperty("member").GetString());
    }

    /// <summary>
    /// AUTHZ-SCOPE-001 and chapter 10 section 2.1: reading the set is
    /// <c>restriction:edit</c>'s, and the support role's <c>restriction:grant</c> does
    /// not reach it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_TheSetIsNotReadWithoutRestrictionEditAsync()
    {
        Browser support = await AuthorisedAsync(Permissions.RestrictionGrant);

        Answer read = await support.SendAsync("GET", "/admin/restrictions/");

        Assert.Equal(StatusCodes.Status403Forbidden, read.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), read.Text("code"));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC3: every edit is a step-up action, a tightening included, so a
    /// session whose proof is no longer recent changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC3_AnEditWithoutStepUpIsRefusedAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer edited = await EditedAsync(administrator, Tighter, reason: null);

        Assert.Equal(StatusCodes.Status403Forbidden, edited.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), edited.Text("code"));
        Assert.Equal(3, (await SmsDestinationAsync()).Buckets[0].Maximum);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC3: a tightening from a session that has proved itself recently
    /// takes effect, needs no reason, and is announced.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC3_ATighteningTakesEffectAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit);

        Answer edited = await EditedAsync(administrator, Tighter, reason: null);

        Assert.Equal(StatusCodes.Status204NoContent, edited.Status);
        Assert.Equal(2, (await SmsDestinationAsync()).Buckets[0].Maximum);
        Assert.False(Assert.Single(_deployment.Events.Of<SendingRestrictionChanged>()).Loosening);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC3 and OPS-CFG-002: a loosening needs a reason.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC3_ALooseningWithoutAReasonIsRefusedAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit, Permissions.SystemAdminister);

        Answer edited = await EditedAsync(administrator, Looser, reason: null);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, edited.Status);
        Assert.Equal(ErrorCodes.RestrictionReasonRequired.ToString(), edited.Text("code"));
        Assert.Equal(3, (await SmsDestinationAsync()).Buckets[0].Maximum);
    }

    /// <summary>
    /// OPS-CFG-002 and chapter 10 section 2.1: a loosening of the set is also
    /// <c>system:administer</c>'s, so <c>restriction:edit</c> alone is refused it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_002_ALooseningOfTheSetRequiresSystemAdministerAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit);

        Answer edited = await EditedAsync(administrator, Looser, "an incident");

        Assert.Equal(StatusCodes.Status403Forbidden, edited.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), edited.Text("code"));
        Assert.Equal(3, (await SmsDestinationAsync()).Buckets[0].Maximum);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC3 and OPS-ALERT-001: a loosening with its reason takes effect
    /// and raises the Normal alert.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC3_ALooseningWithAReasonTakesEffectAndAlertsAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit, Permissions.SystemAdminister);

        Answer edited = await EditedAsync(administrator, Looser, "an incident");

        Assert.Equal(StatusCodes.Status204NoContent, edited.Status);
        Assert.Equal(10, (await SmsDestinationAsync()).Buckets[0].Maximum);
        Assert.Contains(
            _deployment.Events.Of<AlertRaised>(),
            alert => alert.Condition is AlertCondition.RestrictionLoosened);
    }

    /// <summary>
    /// Chapter 09 section 8: an empty bucket list is refused as a value the set does not
    /// admit.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AnEmptyBucketListIsRefusedAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit, Permissions.SystemAdminister);

        Answer edited = await EditedAsync(administrator, Nothing, "no limit");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, edited.Status);
        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed.ToString(), edited.Text("code"));
    }

    /// <summary>
    /// Chapter 09 section 8 and LIB-HOST-001: a host key no supplier answers for is
    /// refused where it is edited, naming the supplier.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_001_AHostKeyWithNoSupplierIsRefusedAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit, Permissions.SystemAdminister);

        Answer edited = await administrator.SendAsync(
            "PUT",
            "/admin/restrictions/tenant.sends",
            ("key", "host:tenant"),
            ("buckets", Tighter));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, edited.Status);
        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed.ToString(), edited.Text("code"));
        Assert.Equal("tenant", edited.Json().GetProperty("details").GetProperty("supplier").GetString());
    }

    /// <summary>
    /// API-CONV-001: a key or a window outside the vocabulary is a malformed request
    /// naming the member.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AnUnreadableRestrictionIsMalformedAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit);

        Answer key = await administrator.SendAsync(
            "PUT",
            "/admin/restrictions/sms.destination",
            ("key", "everyone"),
            ("buckets", Tighter));

        Answer window = await administrator.SendAsync(
            "PUT",
            "/admin/restrictions/sms.destination",
            ("key", "destination"),
            ("buckets", Unwindowed));

        Assert.Equal(StatusCodes.Status400BadRequest, key.Status);
        Assert.Equal("key", key.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, window.Status);
        Assert.Equal("buckets", window.Json().GetProperty("details").GetProperty("member").GetString());
    }

    /// <summary>
    /// Chapter 09 section 8: deleting a shipped default is a loosening, not an error,
    /// and takes it out of the set.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_DeletingAShippedDefaultIsALooseningAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit, Permissions.SystemAdminister);

        Answer unreasoned = await administrator.SendAsync("DELETE", "/admin/restrictions/sms.source", ("reason", null));
        Answer deleted = await administrator.SendAsync("DELETE", "/admin/restrictions/sms.source", ("reason", "a load test"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unreasoned.Status);
        Assert.Equal(StatusCodes.Status204NoContent, deleted.Status);
        Assert.DoesNotContain(await InForceAsync(), one => one.Name == "sms.source");
        Assert.True(Assert.Single(_deployment.Events.Of<SendingRestrictionChanged>()).Loosening);
    }

    /// <summary>
    /// AUTH-ABUSE-004: deleting a name no restriction has changes nothing and is a
    /// malformed request naming <c>name</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_DeletingAnUnknownNameIsMalformedAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit, Permissions.SystemAdminister);

        Answer deleted = await administrator.SendAsync(
            "DELETE",
            "/admin/restrictions/no.such.restriction",
            ("reason", "a tidy set"));

        Assert.Equal(StatusCodes.Status400BadRequest, deleted.Status);
        Assert.Equal("name", deleted.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Empty(_deployment.Events.Of<SendingRestrictionChanged>());
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC4: the support role grants credit with a reason, and the grant
    /// is announced without the key's value.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC4_TheSupportRoleGrantsCreditAsync()
    {
        Browser support = await AuthorisedAsync(Permissions.RestrictionGrant);

        Answer granted = await support.SendAsync(
            "POST",
            "/admin/restrictions/sms.destination/grant",
            ("keyValue", "+201001234567"),
            ("credit", 3),
            ("reason", "their carrier dropped both codes"));

        Assert.Equal(StatusCodes.Status204NoContent, granted.Status);

        SendingRestrictionGranted announced = Assert.Single(_deployment.Events.Of<SendingRestrictionGranted>());

        Assert.Equal(("sms.destination", 3), (announced.Restriction, announced.Credit));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC4: a grant without a reason is refused, and granting is
    /// <c>restriction:grant</c>'s and not <c>restriction:edit</c>'s.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC4_AGrantNeedsItsPermissionAndAReasonAsync()
    {
        Browser support = await AuthorisedAsync(Permissions.RestrictionGrant);

        Answer unreasoned = await support.SendAsync(
            "POST",
            "/admin/restrictions/sms.destination/grant",
            ("keyValue", "+201001234567"),
            ("credit", 3));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unreasoned.Status);
        Assert.Equal(ErrorCodes.RestrictionReasonRequired.ToString(), unreasoned.Text("code"));
        Assert.Empty(_deployment.Events.Of<SendingRestrictionGranted>());
    }

    /// <summary>
    /// AUTHZ-SCOPE-001: <c>restriction:edit</c> does not grant credit.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_EditingDoesNotReachGrantingAsync()
    {
        Browser administrator = await AuthorisedAsync(Permissions.RestrictionEdit);

        Answer granted = await administrator.SendAsync(
            "POST",
            "/admin/restrictions/sms.destination/grant",
            ("keyValue", "+201001234567"),
            ("credit", 3),
            ("reason", "their carrier dropped both codes"));

        Assert.Equal(StatusCodes.Status403Forbidden, granted.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), granted.Text("code"));
    }

    /// <summary>
    /// CONV-CODE-006 AC2 and AUTH-ABUSE-004 AC4: a grant whose body carries no reason is
    /// refused with the reason code before the service is reached, so a caller the
    /// service would refuse for want of the permission is answered for the body, and
    /// no credit is granted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_006_AC2_AGrantMissingItsReasonIsRefusedBeforeTheServiceAsync()
    {
        Browser caller = await AuthorisedAsync();

        Answer unreasoned = await caller.SendAsync(
            "POST",
            "/admin/restrictions/sms.destination/grant",
            ("keyValue", "+201001234567"),
            ("credit", 3),
            ("reason", null));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unreasoned.Status);
        Assert.Equal(ErrorCodes.RestrictionReasonRequired.ToString(), unreasoned.Text("code"));
        Assert.Empty(_deployment.Events.Of<SendingRestrictionGranted>());
    }

    private static Task<Answer> EditedAsync(Browser browser, object[] buckets, string? reason) =>
        browser.SendAsync(
            "PUT",
            "/admin/restrictions/sms.destination",
            ("key", "destination"),
            ("buckets", buckets),
            ("reason", reason));

    private async Task<Restriction> SmsDestinationAsync() =>
        (await InForceAsync()).Single(one => one.Name == "sms.destination");

    private async Task<IReadOnlyList<Restriction>> InForceAsync() =>
        (await _deployment.Configuration.ReadAsync(Settings.Restrictions, TestContext.Current.CancellationToken)).Match(
            value => value,
            error => throw new Xunit.Sdk.XunitException($"The set was refused: {error.Code}."));

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
