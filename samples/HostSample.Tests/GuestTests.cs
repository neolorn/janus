using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Testing;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// Guests who hold no account at the workshop's counter: a guest's details collected
/// under a person record through the library's collection steps and the workshop's own
/// walk-in, the age answered and the consent recorded, a grip fitting registered under
/// the record; the guest reading, exporting and erasing it by a code; and a guest who
/// registers later attaching it (REC-COL-001, REC-COL-002, REC-ACC-002, REC-ATT-001,
/// LIB-TEST-004). The test moves the deployment's clock past the record's deletion
/// window, so it has a deployment of its own.
/// </summary>
/// <param name="host">The deployment.</param>
public sealed partial class GuestTests(WorkshopHost host) : IClassFixture<WorkshopHost>
{
    // The shipped email destination restriction, one send a minute to an address, which
    // the test waits out between two codes to one guest.
    private static readonly TimeSpan PastTheRestriction = TimeSpan.FromMinutes(2);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// REC-COL-002, REC-ACC-002: a guest's walk-in collects the guest's record with the
    /// age answered and the consent to grip fittings recorded, and registers the fitting
    /// under it; by a code to the guest's address the guest reads the record, exports it
    /// with the workshop's section, and erases it, after which the workshop's contact
    /// name for the guest no longer decrypts.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_002_AGuestReadsExportsAndErasesTheirRecordByACodeAsync()
    {
        EmailAddress email = Email("guest.one@kites.example.test");
        string ticket = "kite-" + Guid.NewGuid().ToString("n");

        using TestVisitor counter = host.Guest();

        WalkedIn walkedIn = await WalkedInAsync(counter, email, ticket, "Salma Fawzy", "A thicker wrap for a weak left grip");
        string record = "records/access/" + walkedIn.Subject;

        host.Clock.Advance(PastTheRestriction);

        using TestVisitor guest = host.Guest();

        Answered asked = await Exchanges.SendAsync(guest, HttpMethod.Post, "records/access", new { kind = "email", value = email.Value }, Cancellation);
        Answered opened = await Exchanges.SendAsync(guest, HttpMethod.Post, "records/access/verify", new { code = Code(email) }, Cancellation);
        Answered read = await Exchanges.SendAsync(guest, HttpMethod.Get, record, null, Cancellation);
        Answered exported = await Exchanges.SendAsync(guest, HttpMethod.Get, record + "/export?format=human", null, Cancellation);
        Answered deleting = await Exchanges.SendAsync(guest, HttpMethod.Post, record + "/delete", null, Cancellation);

        host.Clock.Advance(TimeSpan.FromDays(31));

        Answered after = await Exchanges.EventuallyAsync(
            () => Exchanges.SendAsync(host.Technician, HttpMethod.Get, "repairs/" + ticket, null, Cancellation),
            answered => answered.Body is { ValueKind: JsonValueKind.Object } body
                && body.TryGetProperty("contactErased", out JsonElement erased)
                && erased.GetBoolean(),
            Cancellation);

        JsonElement fittings = exported.Body!.Value.GetProperty("sections").EnumerateArray()
            .Single(section => section.GetProperty("name").GetString() == "fittings");

        Assert.True(walkedIn.Created);
        Assert.Equal((HttpStatusCode.Accepted, HttpStatusCode.OK), (asked.Status, opened.Status));
        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Equal("Salma Fawzy", read.Body!.Value.GetProperty("name").GetString());
        Assert.True(read.Body!.Value.GetProperty("age").GetProperty("affirmed").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, exported.Status);
        Assert.Equal(
            "A thicker wrap for a weak left grip",
            Assert.Single(fittings.GetProperty("records").EnumerateArray()).GetProperty("need").GetString());
        Assert.Equal(HttpStatusCode.NoContent, deleting.Status);
        Assert.Equal(HttpStatusCode.OK, after.Status);
        Assert.True(after.Body!.Value.GetProperty("contactErased").GetBoolean());
    }

    /// <summary>
    /// REC-ATT-001: a guest whose walk-in collected a record under an address, and who
    /// registers later with that address, is offered the record by the registration's
    /// proof of the address and attaches it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_001_AGuestWhoRegistersLaterAttachesTheirRecordAsync()
    {
        EmailAddress email = Email("guest.two@kites.example.test");
        string ticket = "kite-" + Guid.NewGuid().ToString("n");

        using TestVisitor counter = host.Guest();

        WalkedIn walkedIn = await WalkedInAsync(counter, email, ticket, "Hoda Samir", "A softer wrap for a sore right grip");

        host.Clock.Advance(PastTheRestriction);

        using TestSession customer = await host.CustomerAsync(email, Cancellation);

        Answered offered = await Exchanges.SendAsync(customer, HttpMethod.Get, "account/records/offer", null, Cancellation);
        Answered attached = await Exchanges.SendAsync(customer, HttpMethod.Post, "account/records/attach", null, Cancellation);
        Answered held = await Exchanges.SendAsync(customer, HttpMethod.Get, "account/records", null, Cancellation);

        Assert.Equal(1, offered.Body!.Value.GetProperty("count").GetInt32());
        Assert.Equal(1, attached.Body!.Value.GetProperty("attached").GetInt32());
        Assert.Equal(
            walkedIn.Subject,
            Assert.Single(held.Body!.Value.EnumerateArray()).GetProperty("subject").GetGuid());
    }

    private static EmailAddress Email(string entered) =>
        EmailAddress.TryParse(entered, out EmailAddress address)
            ? address
            : throw new InvalidOperationException("The address " + entered + " does not read as one.");

    [GeneratedRegex("[0-9]{6}")]
    private static partial Regex SixDigits();

    // The code the latest message to the address carries.
    private string Code(EmailAddress email) =>
        SixDigits().Match(host.Mail.SentTo(email)[^1].Body) is { Success: true } code
            ? code.Value
            : throw new InvalidOperationException("The latest message to " + email + " carries no code.");

    // The guest's screen at the counter: the collection session, the age answered, the
    // address verified by the code sent to it, then the workshop's walk-in, presenting
    // the published notice and terms and the consent to grip fittings ticked.
    private async Task<WalkedIn> WalkedInAsync(
        TestVisitor counter,
        EmailAddress email,
        string ticket,
        string contactName,
        string need)
    {
        Answered notice = await Exchanges.SendAsync(counter, HttpMethod.Get, "privacy/notice", null, Cancellation);
        Answered terms = await Exchanges.SendAsync(counter, HttpMethod.Get, "privacy/documents/" + WorkshopHost.Terms, null, Cancellation);
        Answered begun = await Exchanges.SendAsync(counter, HttpMethod.Post, "records/collection", new { age = true }, Cancellation);
        Answered aged = await Exchanges.SendAsync(counter, HttpMethod.Put, "records/collection/age", new { dateOfBirth = "1990-01-01" }, Cancellation);
        Answered staged = await Exchanges.SendAsync(counter, HttpMethod.Put, "records/collection/email", new { value = email.Value }, Cancellation);
        Answered verified = await Exchanges.SendAsync(
            counter,
            HttpMethod.Post,
            "records/collection/verify",
            new { kind = "email", code = Code(email) },
            Cancellation);
        Answered walkedIn = await Exchanges.SendAsync(
            counter,
            HttpMethod.Post,
            "workshops/" + host.Workshop + "/walk-ins",
            new
            {
                ticket,
                kite = "A foil kite with a torn leading edge",
                contactName,
                need,
                noticeVersion = notice.Body!.Value.GetProperty("version").GetString(),
                termsVersion = terms.Body!.Value.GetProperty("version").GetString(),
                consents = new[] { "adapting grips" },
            },
            Cancellation);

        Assert.Equal(
            (HttpStatusCode.Created, HttpStatusCode.NoContent, HttpStatusCode.Accepted, HttpStatusCode.NoContent),
            (begun.Status, aged.Status, staged.Status, verified.Status));
        Assert.Equal(HttpStatusCode.Created, walkedIn.Status);

        return new WalkedIn(
            walkedIn.Body!.Value.GetProperty("subject").GetGuid(),
            walkedIn.Body!.Value.GetProperty("created").GetBoolean());
    }
}
