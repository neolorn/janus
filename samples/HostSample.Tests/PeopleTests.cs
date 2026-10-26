using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Janus.Testing;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// The people <c>Janus.Testing</c> signs in over the workshop, a deployment it stood up
/// from an empty database, each calling the workshop's own endpoints (LIB-TEST-003 AC1).
/// </summary>
/// <param name="host">The deployment, prepared through <c>TestDeployment</c>.</param>
public sealed class PeopleTests(WorkshopHost host) : IClassFixture<WorkshopHost>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// LIB-TEST-003 AC1: on a deployment <c>TestDeployment</c> prepared, a customer
    /// registered through <c>TestPeople</c> books a repair at the workshop, a staff
    /// member it enrolled through an invitation reads that repair, and the first
    /// administrator it enrolled through the enrolment link lists the workshop's repairs,
    /// each through the workshop's own endpoints and each answered as that person: the
    /// administrator's grant confers no repair, so the list holds none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_AC1_ACustomerTheFirstAdministratorAndAStaffMemberEachCallTheWorkshopsOwnEndpointsAsync()
    {
        using TestSession customer = await host.CustomerAsync(Cancellation);
        string ticket = "kite-" + Guid.NewGuid().ToString("n");

        Answered booked = await Exchanges.SendAsync(
            customer,
            HttpMethod.Post,
            "workshops/" + host.Workshop + "/repairs",
            new { ticket, kite = "A leading-edge inflatable kite with a leaking strut", contactName = "Salma Hosny", collector = (Guid?)null },
            Cancellation);
        Answered read = await Exchanges.SendAsync(host.Technician, HttpMethod.Get, "repairs/" + ticket, null, Cancellation);
        Answered listed = await Exchanges.SendAsync(
            host.Administrator,
            HttpMethod.Get,
            "workshops/" + host.Workshop + "/repairs",
            null,
            Cancellation);

        Assert.Equal(HttpStatusCode.Created, booked.Status);
        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Equal(ticket, read.Body!.Value.GetProperty("ticket").GetString());
        Assert.Equal("Salma Hosny", read.Body!.Value.GetProperty("contactName").GetString());
        Assert.Equal(HttpStatusCode.OK, listed.Status);
        Assert.Empty(listed.Body!.Value.EnumerateArray());
    }
}
