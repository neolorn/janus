using System;
using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Testing;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// What the library owes a host reaching the workshop's own handlers: a message the post
/// room, an alert the duty board, and an outcome the courier reports reaching the library
/// back; and a workshop missing either handler not starting (D-225). The tests read the
/// workshop's own tables, so they have a deployment of their own, with nothing in place
/// of the workshop's handlers.
/// </summary>
/// <param name="host">The deployment.</param>
public sealed class HandlersTests(PostedHost host) : IClassFixture<PostedHost>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// INT-SMS-006 AC1, LIB-TEST-004: a message the library owes leaves it through the
    /// workshop's post room and no other way: a sign-in link asked for an address no
    /// account holds is answered, and the notice it owes that address is a letter in the
    /// post room, with the places it carries.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_SMS_006_AC1_AMessageTheLibraryOwesReachesTheWorkshopsPostRoomAsync()
    {
        const string address = "nobody.flies.here@kites.example.test";

        using TestVisitor guest = host.Guest();

        Answered asked = await Exchanges.SendAsync(guest, HttpMethod.Post, "auth/link", new { identifier = address }, Cancellation);
        Posted? letter = await EventuallyAsync(() => PostedAsync(address));

        Assert.Equal(HttpStatusCode.Accepted, asked.Status);
        Assert.Equal(nameof(MessageKind.NoAccount), letter?.Kind);
        Assert.Equal(nameof(SendKind.Email), letter?.Channel);
    }

    /// <summary>
    /// OPS-ALERT-003 AC1, AC2, LIB-TEST-004: an alert the library raises leaves it through
    /// the workshop's duty board: the absence of an emergency credential, which the
    /// deployment's bootstrap raised, is pinned there once the deployment runs.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AC2_AnAlertTheLibraryRaisesIsPinnedOnTheWorkshopsDutyBoardAsync()
    {
        string? pinned = await EventuallyAsync(() => PinnedAsync(nameof(AlertCondition.NoEmergencyCredential)));

        Assert.Equal(nameof(AlertSeverity.High), pinned);
    }

    /// <summary>
    /// INT-SMS-005 AC2, AC3, LIB-TEST-004: a letter the courier reports it could not
    /// deliver gives back what asking for it counted: a second link asked for the same
    /// address inside the send restriction's interval is refused, and once the courier
    /// reports the first letter failed, the same ask is answered. A report naming no
    /// message of the library's changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_SMS_005_AC2_ALetterTheCourierCouldNotDeliverGivesBackItsCountAsync()
    {
        const string address = "returned.to.sender@kites.example.test";

        using TestVisitor guest = host.Guest();

        Answered first = await Exchanges.SendAsync(guest, HttpMethod.Post, "auth/link", new { identifier = address }, Cancellation);
        Posted? letter = await EventuallyAsync(() => PostedAsync(address));
        Answered refused = await Exchanges.SendAsync(guest, HttpMethod.Post, "auth/link", new { identifier = address }, Cancellation);
        Result<bool> unknown = await host.Courier.ReportAsync("not-a-reference", delivered: false, Cancellation);
        Result<bool> reported = await host.Courier.ReportAsync(letter?.Reference, delivered: false, Cancellation);
        Answered again = await Exchanges.SendAsync(guest, HttpMethod.Post, "auth/link", new { identifier = address }, Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, first.Status);
        Assert.Equal((HttpStatusCode.TooManyRequests, "auth.restriction.exceeded"), (refused.Status, refused.Code));
        Assert.False(unknown.Match(held => held, error => throw new InvalidOperationException(error.Code.ToString())));
        Assert.True(reported.Match(held => held, error => throw new InvalidOperationException(error.Code.ToString())));
        Assert.Equal(HttpStatusCode.Accepted, again.Status);
    }

    /// <summary>
    /// LIB-TEST-004, INT-SMS-005 AC2: once the courier has reported what became of a
    /// letter, the letter and the places it carried are gone from the post room, so
    /// nothing a message carried stays at rest past its outcome: here the code that
    /// verifies an address the first administrator adds to the account.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_ALetterTheCourierReportedLeavesNothingAtRestAsync()
    {
        const string address = "second.address@kites.example.test";

        Answered asked = await Exchanges.SteppedAsync(
            await host.AdministratorAsync(Cancellation),
            HttpMethod.Post,
            "account/identifiers",
            new { kind = "email", value = address },
            Cancellation);
        Posted? letter = await EventuallyAsync(() => PostedAsync(address));
        long carried = await PlacesAsync(letter?.Reference);
        Result<bool> reported = await host.Courier.ReportAsync(letter?.Reference, delivered: true, Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, asked.Status);
        Assert.True(carried > 0);
        Assert.True(reported.Match(held => held, error => throw new InvalidOperationException(error.Code.ToString())));
        Assert.Null(await PostedAsync(address));
        Assert.Equal(0, await PlacesAsync(letter?.Reference));
    }

    /// <summary>
    /// INT-SMS-006 AC2, OPS-ALERT-003 AC4, LIB-TEST-004: the workshop composed without its
    /// post room, or without its duty board, does not start, and the refusal names the
    /// handler missing.
    /// </summary>
    /// <param name="withheld">The handler taken out of the workshop's composition.</param>
    /// <param name="key">What the refusal names.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(typeof(INotificationHandler), "notificationHandler")]
    [InlineData(typeof(IAlertHandler), "alertHandler")]
    public async Task INT_SMS_006_AC2_AWorkshopMissingEitherHandlerDoesNotStartAsync(Type withheld, string key)
    {
        using var serving = new ServerInMemory();
        await using WebApplication application = host.Without(serving, withheld);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await application.StartAsync(Cancellation));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal(key, refused.Failure?.Details["key"].GetString());
    }

    // Reads the workshop's tables until the background work that writes what the test
    // waits for has, or the patience runs out.
    private static async Task<T?> EventuallyAsync<T>(Func<Task<T?>> read)
        where T : class
    {
        DateTimeOffset until = TimeProvider.System.GetUtcNow() + TimeSpan.FromMinutes(2);
        T? found = await read();

        while (found is null && TimeProvider.System.GetUtcNow() < until)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), Cancellation);

            found = await read();
        }

        return found;
    }

    // The letter the post room took for an address, where it took one.
    private async Task<Posted?> PostedAsync(string destination)
    {
        await using DbConnection connection = await host.ConnectAsync(Cancellation);
        await using DbCommand read = connection.CreateCommand();

        read.CommandText = "SELECT reference, kind, channel FROM workshop.post WHERE destination = @destination";
        Named(read, "destination", destination);

        await using DbDataReader rows = await read.ExecuteReaderAsync(Cancellation);

        return await rows.ReadAsync(Cancellation)
            ? new Posted(rows.GetString(0), rows.GetString(1), rows.GetString(2))
            : null;
    }

    // How many places the post room holds for a letter.
    private async Task<long> PlacesAsync(string? reference)
    {
        await using DbConnection connection = await host.ConnectAsync(Cancellation);
        await using DbCommand read = connection.CreateCommand();

        read.CommandText = "SELECT count(*) FROM workshop.post_places WHERE reference = @reference";
        Named(read, "reference", reference ?? string.Empty);

        return (long)(await read.ExecuteScalarAsync(Cancellation) ?? 0L);
    }

    // How urgent the duty board says the alert of a condition is, where one is pinned.
    private async Task<string?> PinnedAsync(string condition)
    {
        await using DbConnection connection = await host.ConnectAsync(Cancellation);
        await using DbCommand read = connection.CreateCommand();

        read.CommandText = "SELECT severity FROM workshop.duty_board WHERE condition = @condition";
        Named(read, "condition", condition);

        return await read.ExecuteScalarAsync(Cancellation) as string;
    }

    private static void Named(DbCommand command, string name, string value)
    {
        DbParameter named = command.CreateParameter();

        named.ParameterName = name;
        named.Value = value;
        _ = command.Parameters.Add(named);
    }

    // A letter in the post room, as the test reads it.
    private sealed record Posted(string Reference, string Kind, string Channel);
}
