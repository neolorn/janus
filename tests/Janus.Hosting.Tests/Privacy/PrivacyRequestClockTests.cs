using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The legal clock of a data subject request as the deployment composes it, over the
/// database, with the calendar in a zone that observes daylight saving (PRIV-RIGHT-002,
/// D-263).
/// </summary>
/// <param name="host">The deployment the cases run against, its calendar in Cairo.</param>
[Trait("kind", "integration")]
public sealed class PrivacyRequestClockTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    /// <summary>
    /// PRIV-RIGHT-002 AC6 (D-263): with the calendar in Cairo, on daylight saving time at
    /// the deployment's instant, an in-app request and an out-of-band one are each
    /// recorded, and the deadline read back is the instant the sixth working day after
    /// the day received ends there, held in UTC.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC6_AnInAppRequestAndAnEnteredOneReadBackTheirDeadlineInUtcAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Authorization.Deployment(host);

        _ = await deployment.BeginAsync([], cancellationToken);

        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        RoleName managing = await deployment.RoleAsync([Permissions.PrivacyRequestManage], cancellationToken);
        SubjectId steward = await deployment.AccountAsync(cancellationToken);
        SubjectId asking = await deployment.AccountAsync(cancellationToken);
        SubjectId writing = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(steward, administrative, cancellationToken);
        _ = await deployment.GrantAsync(
            GrantSubject.Of(steward), managing, null, false, null, administrative, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IPrivacyRequests requests = scope.ServiceProvider.GetRequiredService<IPrivacyRequests>();

        // The deployment's instant, Saturday 19 September at noon UTC, is 15:00 in Cairo,
        // on daylight saving time; the letter reached the company on Tuesday the 15th.
        PrivacyRequestReceipt submitted = Received(await requests.SubmitAsync(
            AccessContext.Of(asking),
            PrivacyRequestType.Restriction,
            "Stop using my address for offers.",
            cancellationToken));
        PrivacyRequestReceipt entered = Received(await requests.EnterAsync(
            AccessContext.Of(steward),
            new PrivacyRequestEntry(
                writing,
                PrivacyRequestType.Rectification,
                "The surname is misspelt.",
                new DateOnly(2026, 9, 15),
                "letter",
                "national identity card seen"),
            cancellationToken));

        IReadOnlyList<PrivacyRequest> queue = (await requests.QueueAsync(AccessContext.Of(steward), cancellationToken))
            .Match(value => value, error => throw new XunitException(error.Code.ToString()));

        DateTimeOffset submittedDue = queue.Single(request => request.Id == submitted.RequestId).DecisionDue;
        DateTimeOffset enteredDue = queue.Single(request => request.Id == entered.RequestId).DecisionDue;

        Assert.NotEqual(Cairo.BaseUtcOffset, Cairo.GetUtcOffset(Authorization.Deployment.Noon));
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 20, 59, 59, TimeSpan.Zero), submittedDue);
        Assert.Equal(new DateTimeOffset(2026, 9, 23, 20, 59, 59, TimeSpan.Zero), enteredDue);
        Assert.Equal(
            (new DateTime(2026, 9, 27, 23, 59, 59), new DateTime(2026, 9, 23, 23, 59, 59)),
            (TimeZoneInfo.ConvertTime(submittedDue, Cairo).DateTime, TimeZoneInfo.ConvertTime(enteredDue, Cairo).DateTime));
        Assert.Equal(
            (submittedDue, TimeSpan.Zero, enteredDue, TimeSpan.Zero),
            (submitted.DecisionDue, submitted.DecisionDue.Offset, entered.DecisionDue, entered.DecisionDue.Offset));
    }

    private static PrivacyRequestReceipt Received(Result<PrivacyRequestReceipt> answered) =>
        answered.Match(value => value, error => throw new XunitException(error.Code.ToString()));
}
