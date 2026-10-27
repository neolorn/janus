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
/// The workshop's job that asks who may read a repair and reads each one's contact
/// details, run outside any request as a host's background work runs (LIB-TEST-004,
/// AUTHZ-GATE-007, IDN-READ-004, D-218).
/// </summary>
/// <param name="host">The deployment.</param>
public sealed class ReadyNoticesTests(WorkshopHost host) : IClassFixture<WorkshopHost>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// LIB-TEST-004, AUTHZ-GATE-007 AC3, IDN-READ-004 AC3 (D-218): the job asks who may
    /// read a repair a customer booked and is answered the technician, whose role reads
    /// every repair of the organization, and the customer, whose booking derives the
    /// reading; it reads each one's contact details, the technician a member of the
    /// organization and the customer the data subject of the repair. The administrator,
    /// whose role reads no repair, is not among them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AJobReadsTheContactDetailsOfEveryoneWhoMayReadARepairAsync()
    {
        using TestSession customer = await host.CustomerAsync(Cancellation);
        string ticket = "kite-" + Guid.NewGuid().ToString("n");

        Answered booked = await Exchanges.SendAsync(
            customer,
            HttpMethod.Post,
            "workshops/" + host.Workshop + "/repairs",
            new { ticket, kite = "A box kite with a torn cell", contactName = "Salma Nour", collector = (Guid?)null },
            Cancellation);
        IReadOnlyDictionary<SubjectId, ContactDetail> recipients = (await host.ReadyNotices
                .RecipientsAsync(host.Organization, ticket, Cancellation))
            .Match(found => found, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(HttpStatusCode.Created, booked.Status);
        Assert.True(new HashSet<SubjectId> { customer.Subject, host.Technician.Subject }.SetEquals(recipients.Keys));
        Assert.Equal("technician@kites.example.test", recipients[host.Technician.Subject].Email);
        Assert.NotNull(recipients[customer.Subject].Email);
    }
}
