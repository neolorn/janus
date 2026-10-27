using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Testing;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// A customer erased once their deletion's grace window has passed, as the person they
/// named to collect their kite sees the repair afterwards (LIB-TEST-004 AC2). The test
/// moves the deployment's clock past the window, so it has a deployment of its own.
/// </summary>
/// <param name="host">The deployment.</param>
public sealed class ErasureTests(WorkshopHost host) : IClassFixture<WorkshopHost>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// LIB-TEST-004 AC2: once the customer who booked a repair is erased, the contact
    /// name the workshop holds under the customer's key no longer decrypts, and the
    /// repair is read without it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_AnErasedCustomersHostFieldNoLongerDecryptsAsync()
    {
        using TestSession customer = await host.CustomerAsync(Cancellation);
        using TestSession collector = await host.CustomerAsync(Cancellation);
        string ticket = "kite-" + Guid.NewGuid().ToString("n");

        Answered booked = await Exchanges.SendAsync(
            customer,
            HttpMethod.Post,
            "workshops/" + host.Workshop + "/repairs",
            new { ticket, kite = "A single-skin kite with a snapped bridle", contactName = "Rania Lotfy", collector = collector.Subject.Value },
            Cancellation);
        Answered before = await Exchanges.SendAsync(collector, HttpMethod.Get, "repairs/" + ticket, null, Cancellation);
        Answered deleting = await Exchanges.SteppedAsync(customer, HttpMethod.Post, "account/delete", null, Cancellation);

        host.Clock.Advance(TimeSpan.FromDays(31));

        Answered after = await Exchanges.EventuallyAsync(
            () => Exchanges.SendAsync(collector, HttpMethod.Get, "repairs/" + ticket, null, Cancellation),
            answered => answered.Body is { ValueKind: JsonValueKind.Object } body
                && body.TryGetProperty("contactErased", out JsonElement erased)
                && erased.GetBoolean(),
            Cancellation);

        Assert.Equal(HttpStatusCode.Created, booked.Status);
        Assert.Equal("Rania Lotfy", before.Body!.Value.GetProperty("contactName").GetString());
        Assert.Equal(HttpStatusCode.Accepted, deleting.Status);
        Assert.Equal(HttpStatusCode.OK, after.Status);
        Assert.True(after.Body!.Value.GetProperty("contactErased").GetBoolean());
        Assert.Null(after.Body!.Value.GetProperty("contactName").GetString());
    }
}
