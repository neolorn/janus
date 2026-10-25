using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Testing;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// The workshop's own endpoints, called as the people <c>Janus.Testing</c> signs in
/// (LIB-TEST-004 AC2).
/// </summary>
/// <param name="host">The deployment.</param>
public sealed class WorkshopEndpointTests(WorkshopHost host) : IClassFixture<WorkshopHost>
{
    // The purposes the workshop declares that a customer's consent or objection changes.
    private const string AdaptingGrips = "adapting grips";

    private const string FollowingUpRepairs = "following up repairs";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// LIB-TEST-004 AC2: a repair booked by a customer is read back with its contact
    /// name, which the workshop's table holds only encrypted under the customer's key.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_ARecordIsWrittenAndReadWithItsPersonalFieldEncryptedAsync()
    {
        using TestSession customer = await host.CustomerAsync(Cancellation);
        string ticket = await BookedAsync(customer, host.Workshop, "Nadia Kamel");

        Answered read = await Exchanges.SendAsync(customer, HttpMethod.Get, "repairs/" + ticket, null, Cancellation);
        byte[] stored = await StoredContactAsync(ticket);

        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Equal("Nadia Kamel", read.Body!.Value.GetProperty("contactName").GetString());
        Assert.False(read.Body!.Value.GetProperty("contactErased").GetBoolean());
        Assert.NotEmpty(stored);
        Assert.Equal(-1, stored.AsSpan().IndexOf(Encoding.UTF8.GetBytes("Nadia Kamel")));
    }

    /// <summary>
    /// LIB-TEST-004 AC2: a workshop's repairs, which need a session, are refused to a
    /// browser holding none, and the catalogue, which answers anyone, is reached by it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_AnEndpointIsRefusedWithoutASessionAndOneMarkedAsAnsweringAnyoneIsReachedAsync()
    {
        using HttpClient visitor = host.Visitor();

        Answered refused = await Exchanges.SendAsync(
            visitor,
            HttpMethod.Get,
            "workshops/" + host.Workshop + "/repairs",
            null,
            Cancellation);
        Answered catalogue = await Exchanges.SendAsync(visitor, HttpMethod.Get, "workshops", null, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.Status);
        Assert.Equal("auth.session.expired", refused.Code);
        Assert.Equal(HttpStatusCode.OK, catalogue.Status);
        Assert.Contains(
            catalogue.Body!.Value.EnumerateArray(),
            workshop => workshop.GetProperty("id").GetString() == host.Workshop);
    }

    /// <summary>
    /// LIB-TEST-004 AC2: releasing a repaired kite, bound to the workshop's step-up gate,
    /// is refused to the technician once the proof of the sign-in is older than the
    /// gate's recency, and done once the technician steps up.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_ABoundActionIsRefusedWithoutItsStepUpAsync()
    {
        using TestSession customer = await host.CustomerAsync(Cancellation);
        string ticket = await BookedAsync(customer, host.Workshop, "Omar Fathy");

        host.Clock.Advance(TimeSpan.FromMinutes(16));

        Answered refused = await Exchanges.SendAsync(
            host.Technician,
            HttpMethod.Post,
            "repairs/" + ticket + "/release",
            null,
            Cancellation);

        await host.Technician.StepUpAsync(Cancellation);

        Answered released = await Exchanges.SendAsync(
            host.Technician,
            HttpMethod.Post,
            "repairs/" + ticket + "/release",
            null,
            Cancellation);
        Answered read = await Exchanges.SendAsync(customer, HttpMethod.Get, "repairs/" + ticket, null, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        Assert.Equal("auth.stepup.required", refused.Code);
        Assert.Equal(HttpStatusCode.NoContent, released.Status);
        Assert.Equal(JsonValueKind.String, read.Body!.Value.GetProperty("releasedAt").ValueKind);
    }

    /// <summary>
    /// LIB-TEST-004 AC2: the organization the first administrator created holds the
    /// first grant they wrote in it under system:administer held in the administrative
    /// organization, and the workshop is registered in it, so that grant reaches it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_ACreatedOrganizationIsGivenItsFirstGrantsAndHoldsTheSamplesRecordsAsync()
    {
        Answered grants = await Exchanges.SteppedAsync(
            host.Administrator,
            HttpMethod.Get,
            "admin/grants?organization=" + host.Organization.Value + "&subjectType=user&subjectId=" + host.Administrator.Subject.Value,
            null,
            Cancellation);
        Answered access = await Exchanges.SteppedAsync(
            host.Administrator,
            HttpMethod.Get,
            "admin/access?resourceType=workshop&resourceId=" + Uri.EscapeDataString(host.Workshop),
            null,
            Cancellation);

        Assert.Equal(HttpStatusCode.OK, grants.Status);
        JsonElement first = Assert.Single(grants.Body!.Value.EnumerateArray());
        Assert.Equal(host.FirstGrant, first.GetProperty("id").GetString());
        Assert.Equal(WorkshopRoles.Manager, first.GetProperty("role").GetString());
        Assert.Equal(host.Organization.Value.ToString(), first.GetProperty("resourceId").GetString());
        Assert.Equal(host.Administrator.Subject.Value.ToString(), first.GetProperty("grantedBy").GetString());
        Assert.Equal(HttpStatusCode.OK, access.Status);
        Assert.Contains(
            access.Body!.Value.GetProperty("grants").EnumerateArray(),
            grant => grant.TryGetProperty("id", out JsonElement id) && id.GetString() == host.FirstGrant);
    }

    /// <summary>
    /// LIB-TEST-004: a workshop's list is filtered by the gate in the workshop's own
    /// query, so a customer sees their own repair and not another's, and each entry
    /// carries the capabilities its reader holds, with what each still requires.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AWorkshopsListIsFilteredByTheGateAndCarriesCapabilitiesAsync()
    {
        using TestSession first = await host.CustomerAsync(Cancellation);
        using TestSession second = await host.CustomerAsync(Cancellation);
        string own = await BookedAsync(first, host.Workshop, "Laila Saad");
        string other = await BookedAsync(second, host.Workshop, "Karim Adel");

        Answered listed = await Exchanges.SendAsync(
            first,
            HttpMethod.Get,
            "workshops/" + host.Workshop + "/repairs",
            null,
            Cancellation);
        Answered technician = await Exchanges.SendAsync(
            host.Technician,
            HttpMethod.Get,
            "workshops/" + host.Workshop + "/repairs",
            null,
            Cancellation);

        JsonElement[] entries = [.. listed.Body!.Value.EnumerateArray()];
        JsonElement owned = Assert.Single(entries);
        JsonElement seen = technician.Body!.Value.EnumerateArray().Single(entry => entry.GetProperty("ticket").GetString() == other);

        Assert.Equal(HttpStatusCode.OK, listed.Status);
        Assert.Equal(own, owned.GetProperty("ticket").GetString());
        Assert.Equal(["cancel", "read"], owned.GetProperty("can").EnumerateArray().Select(action => action.GetString()));
        Assert.Equal("Karim Adel", seen.GetProperty("contactName").GetString());
        Assert.Equal(["read", "release"], seen.GetProperty("can").EnumerateArray().Select(action => action.GetString()));
    }

    /// <summary>
    /// LIB-TEST-004 AC2: a booking past the workshop's capacity is rolled back after the
    /// repair was written and registered, and leaves neither: the workshop's list does
    /// not hold it and the library holds no registration of it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_ARecordRegisteredAndRolledBackLeavesNeitherAsync()
    {
        string workshop = await host.OpenAsync("Harbour kite repairs, the slipway", capacity: 1, Cancellation);
        using TestSession customer = await host.CustomerAsync(Cancellation);
        string kept = await BookedAsync(customer, workshop, "Sara Hany");
        string ticket = Ticket();

        Answered refused = await BookAsync(customer, workshop, ticket, "Sara Hany");
        Answered listed = await Exchanges.SendAsync(
            host.Technician,
            HttpMethod.Get,
            "workshops/" + workshop + "/repairs",
            null,
            Cancellation);
        Answered registered = await AccessAsync(kept);
        Answered unregistered = await AccessAsync(ticket);

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal("workshop.capacity.reached", refused.Code);
        Assert.Equal([kept], listed.Body!.Value.EnumerateArray().Select(entry => entry.GetProperty("ticket").GetString()));
        Assert.Equal(HttpStatusCode.OK, registered.Status);
        Assert.Equal(HttpStatusCode.Forbidden, unregistered.Status);
        Assert.Equal("authz.denied", unregistered.Code);
    }

    /// <summary>
    /// LIB-TEST-004 AC2: a repair its owner cancels is deleted with its registration, so
    /// the library holds no registration of it and the repair is no longer read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_ARegistrationIsRemovedAsync()
    {
        using TestSession customer = await host.CustomerAsync(Cancellation);
        string ticket = await BookedAsync(customer, host.Workshop, "Hana Mourad");
        Answered registered = await AccessAsync(ticket);

        Answered cancelled = await Exchanges.SendAsync(customer, HttpMethod.Delete, "repairs/" + ticket, null, Cancellation);
        Answered removed = await AccessAsync(ticket);
        Answered read = await Exchanges.SendAsync(customer, HttpMethod.Get, "repairs/" + ticket, null, Cancellation);

        Assert.Equal(HttpStatusCode.OK, registered.Status);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.Status);
        Assert.Equal(HttpStatusCode.Forbidden, removed.Status);
        Assert.Equal("authz.denied", removed.Code);
        Assert.Equal(HttpStatusCode.NotFound, read.Status);
    }

    /// <summary>
    /// LIB-TEST-004 AC2: a customer's export carries the workshop's section, with the
    /// customer's repair and its contact name read under the customer's key.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_AnExportCarriesTheHostsSectionAsync()
    {
        using TestSession customer = await host.CustomerAsync(Cancellation);
        string ticket = await BookedAsync(customer, host.Workshop, "Yara Nabil");

        Answered exported = await Exchanges.SteppedAsync(
            customer,
            HttpMethod.Get,
            "privacy/export?format=human",
            null,
            Cancellation);

        JsonElement repairs = exported.Body!.Value.GetProperty("sections").EnumerateArray()
            .Single(section => section.GetProperty("name").GetString() == "repairs");
        JsonElement record = Assert.Single(repairs.GetProperty("records").EnumerateArray());

        Assert.Equal(HttpStatusCode.OK, exported.Status);
        Assert.Equal(ticket, record.GetProperty("ticket").GetString());
        Assert.Equal("Yara Nabil", record.GetProperty("contact-name").GetString());
    }

    /// <summary>
    /// LIB-TEST-004 AC2: a customer's withdrawn consent to grip fittings is handled by
    /// the workshop's purpose handler, which deletes the fittings held for the customer,
    /// so a consent given again finds none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_AWithdrawalIsHandledByTheHandlerAsync()
    {
        using TestSession customer = await host.CustomerAsync(Cancellation);
        string ticket = await BookedAsync(customer, host.Workshop, "Mona Fahmy");
        string purpose = Uri.EscapeDataString(AdaptingGrips);

        Answered granted = await Exchanges.SendAsync(customer, HttpMethod.Post, "privacy/consents/" + purpose + "/grant", null, Cancellation);
        Answered fitted = await Exchanges.SendAsync(
            customer,
            HttpMethod.Post,
            "repairs/" + ticket + "/fittings",
            new { need = "A thicker grip for a weak left hand." },
            Cancellation);
        Answered held = await Exchanges.SendAsync(customer, HttpMethod.Get, "repairs/" + ticket + "/fittings", null, Cancellation);
        Answered withdrawn = await Exchanges.SendAsync(customer, HttpMethod.Post, "privacy/consents/" + purpose + "/withdraw", null, Cancellation);

        Answered settled = await Exchanges.EventuallyAsync(
            () => Exchanges.SteppedAsync(host.Administrator, HttpMethod.Get, "admin/erasures", null, Cancellation),
            answered => answered.Status is HttpStatusCode.OK
                && !answered.Body!.Value.EnumerateArray().Any(delivery =>
                    delivery.GetProperty("kind").GetString() == "consent"
                    && delivery.GetProperty("subject").GetString() == customer.Subject.ToString()),
            Cancellation);

        Answered regranted = await Exchanges.SendAsync(customer, HttpMethod.Post, "privacy/consents/" + purpose + "/grant", null, Cancellation);
        Answered left = await Exchanges.SendAsync(customer, HttpMethod.Get, "repairs/" + ticket + "/fittings", null, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, granted.Status);
        Assert.Equal(HttpStatusCode.Created, fitted.Status);
        Assert.Equal("A thicker grip for a weak left hand.", Assert.Single(held.Body!.Value.EnumerateArray()).GetProperty("need").GetString());
        Assert.Equal(HttpStatusCode.NoContent, withdrawn.Status);
        Assert.Equal(HttpStatusCode.OK, settled.Status);
        Assert.Equal(HttpStatusCode.NoContent, regranted.Status);
        Assert.Equal(HttpStatusCode.OK, left.Status);
        Assert.Empty(left.Body!.Value.EnumerateArray());
    }

    /// <summary>
    /// LIB-TEST-004 AC2: a customer's objection to follow-up calls is handled by the
    /// workshop's purpose handler, which stops them for the customer's repairs.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_AnObjectionIsHandledByTheHandlerAsync()
    {
        using TestSession customer = await host.CustomerAsync(Cancellation);
        string ticket = await BookedAsync(customer, host.Workshop, "Tarek Samir");
        Answered before = await Exchanges.SendAsync(customer, HttpMethod.Get, "repairs/" + ticket, null, Cancellation);

        Answered objected = await Exchanges.SendAsync(
            customer,
            HttpMethod.Post,
            "privacy/objections/" + Uri.EscapeDataString(FollowingUpRepairs),
            null,
            Cancellation);
        Answered after = await Exchanges.EventuallyAsync(
            () => Exchanges.SendAsync(customer, HttpMethod.Get, "repairs/" + ticket, null, Cancellation),
            answered => answered.Body is { ValueKind: JsonValueKind.Object } body
                && body.TryGetProperty("followUp", out JsonElement followUp)
                && !followUp.GetBoolean(),
            Cancellation);

        Assert.True(before.Body!.Value.GetProperty("followUp").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, objected.Status);
        Assert.False(after.Body!.Value.GetProperty("followUp").GetBoolean());
    }

    private static string Ticket() => "kite-" + Guid.NewGuid().ToString("n");

    private static Task<Answered> BookAsync(TestSession customer, string workshop, string ticket, string contactName) =>
        Exchanges.SendAsync(
            customer,
            HttpMethod.Post,
            "workshops/" + workshop + "/repairs",
            new { ticket, kite = "A two-line foil kite with a torn leading edge", contactName, collector = (Guid?)null },
            Cancellation);

    private static async Task<string> BookedAsync(TestSession customer, string workshop, string contactName)
    {
        string ticket = Ticket();
        Answered booked = await BookAsync(customer, workshop, ticket, contactName);

        Assert.Equal(HttpStatusCode.Created, booked.Status);

        return ticket;
    }

    // Who can reach a repair, as the first administrator asks the library; a record the
    // library holds no registration of is refused as one outside the administrator's
    // reach (09 GET /admin/access).
    private Task<Answered> AccessAsync(string ticket) =>
        Exchanges.SteppedAsync(
            host.Administrator,
            HttpMethod.Get,
            "admin/access?resourceType=repair&resourceId=" + Uri.EscapeDataString(ticket),
            null,
            Cancellation);

    // The contact name as the workshop's table holds it.
    private async Task<byte[]> StoredContactAsync(string ticket)
    {
        await using DbConnection connection = await host.ConnectAsync(Cancellation);
        await using DbCommand read = connection.CreateCommand();

        read.CommandText = "SELECT contact_name FROM workshop.repairs WHERE id = @ticket";

        DbParameter named = read.CreateParameter();

        named.ParameterName = "ticket";
        named.Value = ticket;
        _ = read.Parameters.Add(named);

        return (byte[])(await read.ExecuteScalarAsync(Cancellation))!;
    }
}
