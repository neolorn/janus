using System;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Testing;
using Xunit;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's web server ends each request as a web server ends one, so what the
/// request's scope holds, the library's context and its connection among it, is
/// released with it.
/// </summary>
/// <param name="host">The sample host.</param>
[Trait("kind", "integration")]
public sealed class ServerInMemoryTests(SampleHost host) : IClassFixture<SampleHost>
{
    private const string Password = "quietly-folded-heron-lantern-48";

    private const int Customers = 5;

    /// <summary>
    /// Customers registered and signed in one after another through the library's own
    /// endpoints leave the deployment's connections to its database where the first
    /// customer left them, give or take fewer than one connection for each.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task SendAsync_CustomersRegisteredOneAfterAnother_LeaveTheDeploymentsConnectionsBoundedAsync()
    {
        await PublishedAsync();
        (await RegisteredAsync(0)).Dispose();
        long first = await ConnectionsAsync();

        for (int customer = 1; customer <= Customers; customer++)
        {
            (await RegisteredAsync(customer)).Dispose();
        }

        long last = await ConnectionsAsync();

        Assert.InRange(last - first, long.MinValue, Customers - 1);
    }

    // One customer, registered and signed in through the library's own endpoints.
    private async Task<TestSession> RegisteredAsync(int customer) =>
        await host.People.CustomerAsync(
            EmailAddress.TryParse(
                "customer." + customer.ToString(CultureInfo.InvariantCulture) + "@sample.example.test",
                out EmailAddress email)
                ? email
                : throw new InvalidOperationException("The address does not read as one."),
            PhoneNumber.TryParse(
                "+20100000005" + customer.ToString(CultureInfo.InvariantCulture),
                out PhoneNumber phone)
                ? phone
                : throw new InvalidOperationException("The number does not read as one."),
            Password,
            TestContext.Current.CancellationToken);

    // The connections the deployment holds to its database, this one not counted.
    private async Task<long> ConnectionsAsync()
    {
        await using DbConnection connection = await host.ConnectAsync(TestContext.Current.CancellationToken);

        return await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid()");
    }

    // The notice and the terms a registration accepts, published by the administrator.
    private async Task PublishedAsync()
    {
        using HttpResponseMessage notice = await host.Administrator.PostAsJsonAsync(
            new Uri("admin/notices", UriKind.Relative),
            new { text = "The privacy notice.", material = false },
            TestContext.Current.CancellationToken);
        using HttpResponseMessage terms = await host.Administrator.PostAsJsonAsync(
            new Uri("admin/documents/" + SampleHost.Terms + "/versions", UriKind.Relative),
            new { text = "The terms of service.", material = false },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, notice.StatusCode);
        Assert.Equal(HttpStatusCode.OK, terms.StatusCode);
    }
}
