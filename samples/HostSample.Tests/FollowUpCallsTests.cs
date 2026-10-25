using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Testing;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// The workshop's job, run outside any request as a host's background work runs
/// (LIB-TEST-004 AC2).
/// </summary>
/// <param name="host">The deployment.</param>
public sealed class FollowUpCallsTests(WorkshopHost host) : IClassFixture<WorkshopHost>
{
    // The permission the job declares, and one of the workshop's it does not.
    private static readonly Permission ReadRepair = Permission.Parse("repair:read");

    private static readonly Permission CancelRepair = Permission.Parse("repair:cancel");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// LIB-TEST-004 AC2: the job is admitted under the permission it declares, reading
    /// the repair a customer booked in its organization and admitted to read repairs
    /// across it, and refused cancelling them, a permission it does not declare.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_AJobIsAdmittedForItsDeclaredPermissionAndRefusedForAnotherAsync()
    {
        using TestSession customer = await host.CustomerAsync(Cancellation);
        string ticket = "kite-" + Guid.NewGuid().ToString("n");
        AccessContext job = FollowUpCalls.ContextFor(host.Organization);

        Answered booked = await Exchanges.SendAsync(
            customer,
            HttpMethod.Post,
            "workshops/" + host.Workshop + "/repairs",
            new { ticket, kite = "A delta kite with a split spar", contactName = "Karim Adel", collector = (Guid?)null },
            Cancellation);
        Result<IReadOnlyList<string>> due = await host.FollowUpCalls.DueAsync(host.Organization, Cancellation);
        Result reading = await host.RequireAsync(job, ReadRepair, host.Organization, Cancellation);
        Result cancelling = await host.RequireAsync(job, CancelRepair, host.Organization, Cancellation);

        Assert.Equal(HttpStatusCode.Created, booked.Status);
        Assert.Contains(ticket, due.Match(tickets => tickets, error => throw new InvalidOperationException(error.Code.ToString())));
        Assert.True(reading.Match(() => true, _ => false));
        Assert.Equal(ErrorCodes.Denied, cancelling.Match<ErrorCode?>(() => null, error => error.Code));
    }
}
