using System;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Janus.Testing;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// Bookings of a workshop's last place made at once (CONV-DESIGN-003). The test registers
/// customers of its own, so it has a deployment of its own.
/// </summary>
/// <param name="host">The deployment.</param>
public sealed class CapacityTests(WorkshopHost host) : IClassFixture<WorkshopHost>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// CONV-DESIGN-003 AC6: two customers booking a workshop's last place at once leave
    /// the outcome of one booking after the other: one booked, the other refused for the
    /// workshop's capacity. The test holds the workshop's row until both bookings wait on
    /// it, so both reach the workshop at once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC6_TwoBookingsOfAWorkshopsLastPlaceLeaveOneAsync()
    {
        string workshop = await host.OpenAsync("Harbour kite repairs, the boathouse", capacity: 1, Cancellation);
        using TestSession first = await host.CustomerAsync(Cancellation);
        using TestSession second = await host.CustomerAsync(Cancellation);
        (Answered[] booked, bool bothWaited) = await BookedTogetherAsync(workshop, first, second);

        Answered listed = await Exchanges.SendAsync(
            host.Technician,
            HttpMethod.Get,
            "workshops/" + workshop + "/repairs",
            null,
            Cancellation);

        Assert.True(bothWaited);
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], booked.Select(answered => answered.Status).Order());
        Assert.Equal("workshop.capacity.reached", Assert.Single(booked, answered => answered.Status is HttpStatusCode.Conflict).Code);
        _ = Assert.Single(listed.Body!.Value.EnumerateArray());
    }

    private static string Ticket() => "kite-" + Guid.NewGuid().ToString("n");

    private static Task<Answered> BookAsync(TestSession customer, string workshop, string ticket, string contactName) =>
        Exchanges.SendAsync(
            customer,
            HttpMethod.Post,
            "workshops/" + workshop + "/repairs",
            new { ticket, kite = "A two-line foil kite with a torn leading edge", contactName, collector = (Guid?)null },
            Cancellation);

    // Two customers' bookings of one workshop made at once: the workshop's row is held
    // until both wait on it, then let go, and what each was answered, with whether both
    // waited.
    private async Task<(Answered[] Booked, bool BothWaited)> BookedTogetherAsync(
        string workshop,
        TestSession first,
        TestSession second)
    {
        await using DbConnection held = await host.ConnectAsync(Cancellation);
        await using DbTransaction holding = await held.BeginTransactionAsync(Cancellation);
        await using (DbCommand locked = held.CreateCommand())
        {
            locked.Transaction = holding;
            locked.CommandText = "SELECT 1 FROM workshop.workshops WHERE id = @workshop FOR UPDATE";

            DbParameter named = locked.CreateParameter();

            named.ParameterName = "workshop";
            named.Value = workshop;
            _ = locked.Parameters.Add(named);
            _ = await locked.ExecuteNonQueryAsync(Cancellation);
        }

        Task<Answered[]> booking = Task.WhenAll(
            BookAsync(first, workshop, Ticket(), "Dina Ragab"),
            BookAsync(second, workshop, Ticket(), "Hesham Badr"));

        bool bothWaited = await WaitingAsync(held, 2);

        await holding.CommitAsync(Cancellation);

        return (await booking, bothWaited);
    }

    // Whether as many of the deployment's connections as asked wait on a lock, looked at
    // until they do or the test's patience runs out.
    private static async Task<bool> WaitingAsync(DbConnection connection, int waiting)
    {
        DateTimeOffset until = TimeProvider.System.GetUtcNow() + TimeSpan.FromMinutes(2);

        await using DbCommand counted = connection.CreateCommand();

        counted.CommandText = "SELECT count(*) FROM pg_stat_activity "
            + "WHERE datname = current_database() AND wait_event_type = 'Lock'";

        while (Convert.ToInt32(await counted.ExecuteScalarAsync(Cancellation), CultureInfo.InvariantCulture) < waiting)
        {
            if (TimeProvider.System.GetUtcNow() >= until)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Cancellation);
        }

        return true;
    }
}
