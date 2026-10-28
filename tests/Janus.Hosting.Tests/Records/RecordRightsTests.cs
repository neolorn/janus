using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Records;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Records;

/// <summary>
/// A person exercising their rights on a record a record access session reaches, with
/// no account and no human between them and the answer (REC-ACC-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecordRightsTests : IAsyncDisposable
{
    private const string Address = "guest@example.test";

    private const string Language = "en";

    private const string DeletionNotice = "deleting";

    private const string Marketing = "marketing";

    private const string Security = "security";

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    private readonly Deployment _deployment = new();

    private readonly OrganizationId _shop;

    private readonly SubjectId _record;

    /// <summary>
    /// A deployment whose record access and deletion messages carry a fixed text, with
    /// one record collected in an organization holding a verified email.
    /// </summary>
    public RecordRightsTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Configuration.Set(Settings.NotificationLanguages, [Language, "ar"]);
        _deployment.Configuration.Set(Settings.PrivacyCalendarTimeZone, "Africa/Cairo");
        _deployment.Documents.Hold(new DocumentVersion("privacy-notice", "1", "ar", "النص", [], _deployment.Clock.GetUtcNow()));

        _deployment.Templates.Set(MessageKind.RecordAccessCode, SendKind.Email, Language, new MessageTemplate("code", "{code}"));
        foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
        {
            _deployment.Templates.Set(
                MessageKind.RecordDeletionNotice,
                kind,
                Language,
                new MessageTemplate(kind is SendKind.Email ? "deleting" : null, DeletionNotice));
        }

        _shop = OrganizationId.New(_deployment.Clock);
        _deployment.Organizations.Seed(_shop, name: "Seaside Rentals");

        _record = SubjectId.New(Randomness);
        DateTimeOffset verified = _deployment.Clock.GetUtcNow() - TimeSpan.FromDays(1);

        _deployment.Records.Hold(
            new HeldRecord(_record, _shop, verified)
            {
                Language = Language,
                Name = DisplayName.TryParse("Mona Saleh", out DisplayName name) ? name : null,
                Age = new CollectedAge(AdultAffirmed: true, Group: null, new DateOnly(1990, 4, 1), verified),
                Contacts = { [IdentifierKind.Email] = new CollectedContact(IdentifierKind.Email, Address, verified) },
                Documents = [("1", "3", verified)],
            });
        _deployment.AccountStates.Hold(_record, AccountState.Active);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REC-ACC-002 AC1: a person reaching a record by a code reads it, exports it in both
    /// formats with each contributor's sections, withdraws a consent and objects to a
    /// purpose, each without contacting anyone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_002_AC1_AReachedRecordIsReadExportedAndItsConsentsChangedAsync()
    {
        _deployment.ExportSource.Holds(_record, Section("record", "subject", _record.ToString()));
        _deployment.ExportContributor.Sections = [Section("bookings", "reference", "bk-2201")];

        Browser browser = await OpenedAsync();
        int sent = _deployment.Mail.Taken.Count;

        Answer read = await browser.SendAsync("GET", Rights());
        Answer readable = await browser.SendAsync("GET", Rights("/export?format=human"));
        Answer portable = await browser.SendAsync("GET", Rights("/export?format=machine"));
        Answer granted = await browser.SendAsync("POST", Rights("/consents/" + Marketing + "/grant"));
        Answer withdrawn = await browser.SendAsync("POST", Rights("/consents/" + Marketing + "/withdraw"));
        Answer objected = await browser.SendAsync("POST", Rights("/objections/" + Security));

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal(
            [StatusCodes.Status200OK, StatusCodes.Status200OK, StatusCodes.Status204NoContent, StatusCodes.Status204NoContent, StatusCodes.Status204NoContent],
            new[] { readable, portable, granted, withdrawn, objected }.Select(answer => answer.Status));
        Assert.Equal(
            ["record", "consents", "objections", "bookings"],
            readable.Json().GetProperty("sections").EnumerateArray().Select(section => section.GetProperty("name").GetString()));
        Assert.Equal("bk-2201", portable.Json().GetProperty("values").GetProperty("bookings.0.reference").GetString());

        JsonElement consent = Assert.Single((await browser.SendAsync("GET", Rights("/consents"))).Json().EnumerateArray());
        JsonElement objection = Assert.Single((await browser.SendAsync("GET", Rights("/objections"))).Json().EnumerateArray());

        Assert.NotEqual(JsonValueKind.Null, consent.GetProperty("withdrawnAt").ValueKind);
        Assert.Equal(Security, objection.GetProperty("purpose").GetString());
        Assert.Equal([_record, _record], _deployment.ExportLedger.Taken.Select(taken => taken.Subject));
        Assert.Equal(sent, _deployment.Mail.Taken.Count);
    }

    /// <summary>
    /// REC-ACC-002: the read gives the record's organization by name, its contacts with
    /// when each was verified, its age answer without the date of birth, and the notice
    /// and terms versions its collection recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_002_TheReadGivesTheRecordWithoutItsDateOfBirthAsync()
    {
        Browser browser = await OpenedAsync();

        JsonElement read = (await browser.SendAsync("GET", Rights())).Json();
        HeldRecord held = _deployment.Records.Of(_record);

        Assert.Equal(_record.ToString(), read.GetProperty("subject").GetString());
        Assert.Equal("Seaside Rentals", read.GetProperty("organizationName").GetString());
        Assert.Equal("collection", read.GetProperty("origin").GetString());
        Assert.Equal("active", read.GetProperty("state").GetString());
        Assert.Equal("Mona Saleh", read.GetProperty("name").GetString());
        Assert.Equal(Language, read.GetProperty("language").GetString());

        JsonElement contact = Assert.Single(read.GetProperty("contacts").EnumerateArray());

        Assert.Equal((Address, held.Contacts[IdentifierKind.Email].VerifiedAt), (contact.GetProperty("value").GetString(), contact.GetProperty("verifiedAt").GetDateTimeOffset()));
        Assert.True(read.GetProperty("age").GetProperty("affirmed").GetBoolean());
        Assert.DoesNotContain(read.GetProperty("age").EnumerateObject(), member => member.Name.Contains("birth", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("1", Assert.Single(read.GetProperty("notices").EnumerateArray()).GetProperty("version").GetString());
        Assert.Equal("3", Assert.Single(read.GetProperty("terms").EnumerateArray()).GetProperty("version").GetString());
    }

    /// <summary>
    /// REC-ACC-002: a rectification changes the record's name and language, each change
    /// recorded with the record acting; a language outside <c>notification.languages</c>
    /// is refused naming it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_002_ARectificationChangesTheNameAndLanguageAsync()
    {
        Browser browser = await OpenedAsync();

        Answer changed = await browser.SendAsync("PUT", Rights(), ("name", "Mona S."), ("language", "ar"));
        Answer outside = await browser.SendAsync("PUT", Rights(), ("language", "xx"));

        Assert.Equal(StatusCodes.Status204NoContent, changed.Status);
        Assert.Equal(("Mona S.", "ar"), (_deployment.Records.Of(_record).Name?.Value, _deployment.Records.Of(_record).Language));
        Assert.Equal(
            [(_record, _record, "name"), (_record, _record, "language")],
            _deployment.RecordAudit.Changed.Select(change => (change.Acting, change.Record, change.Field)));
        Assert.Equal(
            (StatusCodes.Status422UnprocessableEntity, ErrorCodes.RequestInvalid.ToString()),
            (outside.Status, outside.Text("code")));
    }

    /// <summary>
    /// REC-ACC-002 AC2: a record's deletion asked by a code begins its window and sends
    /// <c>record-deletion-notice</c>; a new code reaches it to cancel inside the window,
    /// which returns it as it was.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_002_AC2_ADeletionByACodeIsCancelledByANewCodeAsync()
    {
        Browser asking = await OpenedAsync();

        // Past the shipped one-a-minute restriction of the address the code counted against.
        _deployment.Clock.Advance(TimeSpan.FromMinutes(2));

        Answer deleted = await asking.SendAsync("POST", Rights("/delete"));

        _ = await _deployment.CarrySendsAsync();

        HeldRecord deleting = _deployment.Records.Of(_record);

        Assert.Equal(StatusCodes.Status204NoContent, deleted.Status);
        Assert.Equal((PersonRecordState.Deleting, DeletionOrigin.Self), (deleting.State, deleting.DeletingBy));
        Assert.Equal(DeletionNotice, Assert.Single(_deployment.Mail.Taken, mail => mail.Body == DeletionNotice).Body);
        Assert.Equal(
            _deployment.Clock.GetUtcNow() + TimeSpan.FromDays(30),
            Assert.Single(_deployment.Events.Of<RecordDeletionRequested>()).ErasesAt);

        // Chapter 09 section 7a: while its own deletion runs, the record answers its read
        // and its cancellation alone.
        Answer exported = await asking.SendAsync("GET", Rights("/export?format=human"));

        Assert.Equal(
            (StatusCodes.Status409Conflict, ErrorCodes.RecordStateConflict.ToString()),
            (exported.Status, exported.Text("code")));

        _deployment.Clock.Advance(TimeSpan.FromDays(10));

        Browser returning = await OpenedAsync();

        Answer cancelled = await returning.SendAsync("POST", Rights("/delete/cancel"));

        Assert.Equal(StatusCodes.Status204NoContent, cancelled.Status);
        Assert.Equal(PersonRecordState.Active, _deployment.Records.Of(_record).State);
        _ = Assert.Single(_deployment.Events.Of<RecordDeletionCancelled>());
    }

    /// <summary>
    /// REC-ACC-002 AC2: a cancellation once the window has closed is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_002_AC2_ACancellationAfterTheWindowIsRefusedAsync()
    {
        Browser asking = await OpenedAsync();

        Assert.Equal(StatusCodes.Status204NoContent, (await asking.SendAsync("POST", Rights("/delete"))).Status);

        _deployment.Clock.Advance(TimeSpan.FromDays(31));

        Browser late = await OpenedAsync();

        Answer refused = await late.SendAsync("POST", Rights("/delete/cancel"));

        Assert.Equal(
            (StatusCodes.Status422UnprocessableEntity, ErrorCodes.DeletionWindowElapsed.ToString()),
            (refused.Status, refused.Text("code")));
    }

    /// <summary>
    /// REC-ACC-002 AC3: a <c>restricted</c> record's name change is refused with
    /// <c>authz.restricted</c>, and its export is served.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_002_AC3_ARestrictedRecordIsExportedButNotChangedAsync()
    {
        _deployment.Records.Of(_record).State = PersonRecordState.Restricted;

        Browser browser = await OpenedAsync();

        Answer changed = await browser.SendAsync("PUT", Rights(), ("name", "Someone Else"));
        Answer exported = await browser.SendAsync("GET", Rights("/export?format=machine"));

        Assert.Equal(
            (StatusCodes.Status403Forbidden, ErrorCodes.Restricted.ToString()),
            (changed.Status, changed.Text("code")));
        Assert.Equal("Mona Saleh", _deployment.Records.Of(_record).Name?.Value);
        Assert.Equal(StatusCodes.Status200OK, exported.Status);
    }

    /// <summary>
    /// REC-ACC-002: a request entered on a record is received as the account's own is,
    /// its receipt told to the record.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_002_ARequestOnARecordIsReceivedAsync()
    {
        Browser browser = await OpenedAsync();

        Answer received = await browser.SendAsync(
            "POST",
            Rights("/requests"),
            ("type", "restriction"),
            ("detail", "Please stop using my details."));

        Assert.Equal(StatusCodes.Status202Accepted, received.Status);
        Assert.Equal(_record, Assert.Single(_deployment.Requests.Queue).Subject);
        Assert.Contains(_deployment.Notices.Told, told => told.Subject == _record);
    }

    /// <summary>
    /// REC-ACC-002, chapter 09 section 7a: a record the session does not reach is
    /// answered as one no record bears; a browser with no record access session is
    /// refused as expired; a signed-in browser is refused first.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_002_ARecordOutOfReachIsAnsweredAsNoneAsync()
    {
        var unreached = SubjectId.New(Randomness);

        _deployment.Records.Hold(new HeldRecord(unreached, _shop, _deployment.Clock.GetUtcNow()) { Language = Language });

        Browser browser = await OpenedAsync();
        var stranger = new Browser(_deployment);

        _ = await stranger.SendAsync("GET", "/records/access");

        Browser signedIn = await Flow.SignedInAsync(_deployment);

        Answer missed = await browser.SendAsync("GET", "/records/access/" + unreached);
        Answer expired = await stranger.SendAsync("GET", Rights());
        Answer refused = await signedIn.SendAsync("GET", Rights());

        Assert.Equal(
            [
                (StatusCodes.Status404NotFound, ErrorCodes.RecordNotFound.ToString()),
                (StatusCodes.Status401Unauthorized, ErrorCodes.SessionExpired.ToString()),
                (StatusCodes.Status409Conflict, ErrorCodes.RecordSignedIn.ToString()),
            ],
            new[] { missed, expired, refused }.Select(answer => (answer.Status, answer.Text("code"))));
    }

    private static ExportSection Section(string name, string field, string value) =>
        new(name, [new ExportRecord(new Dictionary<string, string>(StringComparer.Ordinal) { [field] = value })]);

    private string Rights(string path = "") => "/records/access/" + _record + path;

    // A browser that asked for a code to the record's email and opened its session with
    // it, past the shipped one-a-minute restriction of the address.
    private async Task<Browser> OpenedAsync()
    {
        _deployment.Clock.Advance(TimeSpan.FromMinutes(2));

        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/records/access");

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("POST", "/records/access", ("kind", "email"), ("value", Address))).Status);

        _ = await _deployment.CarrySendsAsync();

        Answer opened = await browser.SendAsync("POST", "/records/access/verify", ("code", Flow.Code(_deployment, IdentifierKind.Email)));

        Assert.Equal(StatusCodes.Status200OK, opened.Status);

        return browser;
    }
}
