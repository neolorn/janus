using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Records;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Records;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Records;

/// <summary>
/// A person reaching the records that hold their email or phone by a code sent to it,
/// through a record access session (REC-ACC-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecordAccessTests : IAsyncDisposable
{
    private const string Address = "guest@example.test";

    private const string Number = "+441632960041";

    private const string NoneSent = "none";

    private const string Language = "en";

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    private readonly Deployment _deployment = new();

    private readonly OrganizationId _shop;

    /// <summary>
    /// A deployment whose record access messages carry the code alone, with an
    /// organization a host keeps guests' details in.
    /// </summary>
    public RecordAccessTests() => _shop = Prepared(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REC-ACC-001 AC1: an ask for an address a reachable record holds and one for an
    /// address none holds are answered in identical bytes; the first sends a code, the
    /// second <c>record-access-none</c>, once per window.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_001_AC1_AnAskIsAnsweredAlikeWhateverIsHeldAsync()
    {
        const string unheld = "nobody@example.test";

        _ = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1));

        Answer held = await AskedAsync(new Browser(_deployment), "email", Address);
        Answer none = await AskedAsync(new Browser(_deployment), "email", unheld);

        // Past the shipped one-a-minute restriction of an address, inside the window.
        _deployment.Clock.Advance(TimeSpan.FromMinutes(2));

        Answer again = await AskedAsync(new Browser(_deployment), "email", unheld);

        _ = await _deployment.CarrySendsAsync();

        Assert.Equal(StatusCodes.Status202Accepted, held.Status);
        Assert.Equal((held.Status, held.Body), (none.Status, none.Body));
        Assert.Equal((held.Status, held.Body), (again.Status, again.Body));
        Assert.Matches("^[0-9]{6}$", Assert.Single(_deployment.Mail.Taken, sent => sent.Destination.Value == Address).Body);
        Assert.Equal(NoneSent, Assert.Single(_deployment.Mail.Taken, sent => sent.Destination.Value == unheld).Body);
    }

    /// <summary>
    /// REC-ACC-001 AC2: a record whose phone was verified at a collection inside
    /// <c>records.proof.age</c> is reached by the code alone; one verified longer ago is
    /// reached only once its reference is given; a reference that matches nothing reaches
    /// nothing and is counted against the value.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_001_AC2_AnOlderRecordIsReachedOnlyByItsReferenceAsync()
    {
        SubjectId recent = Held(IdentifierKind.Phone, Number, verifiedAgo: TimeSpan.FromDays(1));
        SubjectId older = Held(IdentifierKind.Phone, Number, verifiedAgo: TimeSpan.FromDays(200), reference: "order-7731");

        Browser browser = await OpenedAsync("phone", Number);

        Assert.Equal([recent], Reached(await browser.SendAsync("GET", "/records/access")));

        Answer missed = await browser.SendAsync("POST", "/records/access/reference", ("reference", "order-0000"));

        Assert.Equal(StatusCodes.Status200OK, missed.Status);
        Assert.Equal([recent], Reached(missed));
        Assert.Contains((await LedgerAsync()).Failures, failure => failure.Scope is not ThrottleScope.Source);

        Answer matched = await browser.SendAsync("POST", "/records/access/reference", ("reference", "  ORDER-7731 "));

        Assert.Equal(
            new[] { recent, older }.OrderBy(subject => subject.Value),
            Reached(matched).OrderBy(subject => subject.Value));
        Assert.Equal(
            new[] { (recent, false), (older, true) },
            _deployment.RecordAudit.Accessed.Select(entry => (entry.Record, entry.ByReference)));
    }

    /// <summary>
    /// REC-ACC-001 AC3: a record staff entered, one attached to an account, one taken
    /// down, one deleting out of band and one deleted are never reached, by the code or
    /// by a reference.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_001_AC3_RecordsOutOfReachAreNeverReachedAsync()
    {
        const string reference = "order-5501";

        SubjectId reachable = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1), reference: reference);
        SubjectId entered = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1), reference: reference);
        SubjectId attached = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1), reference: reference);
        SubjectId takenDown = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1), reference: reference);
        SubjectId outOfBand = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1), reference: reference);
        SubjectId deleted = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1), reference: reference);

        _deployment.Records.Hold(_deployment.Records.Of(entered) with { Origin = PersonRecordOrigin.Staff });
        _deployment.Records.Of(attached).AttachedTo = SubjectId.New(Randomness);
        (_deployment.Records.Of(takenDown).State, _deployment.Records.Of(takenDown).DeletingBy) =
            (PersonRecordState.Deleting, DeletionOrigin.Takedown);
        (_deployment.Records.Of(outOfBand).State, _deployment.Records.Of(outOfBand).DeletingBy) =
            (PersonRecordState.Deleting, DeletionOrigin.OutOfBandRequest);
        _deployment.Records.Of(deleted).State = PersonRecordState.Deleted;

        Browser browser = await OpenedAsync("email", Address);

        Answer referenced = await browser.SendAsync("POST", "/records/access/reference", ("reference", reference));

        Assert.Equal([reachable], Reached(referenced));
    }

    /// <summary>
    /// REC-ATT-002 AC3: a code for the contact an attached record holds opens a session
    /// that does not reach it, in the state or by its own path; a contact only an attached
    /// record holds is sent <c>record-access-none</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_002_AC3_ACodeForAnAttachedRecordsContactDoesNotReachItAsync()
    {
        const string alone = "attached.only@example.test";

        SubjectId reachable = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1));
        SubjectId attached = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1));
        SubjectId only = Held(IdentifierKind.Email, alone, verifiedAgo: TimeSpan.FromDays(1));

        _deployment.Records.Of(attached).AttachedTo = SubjectId.New(Randomness);
        _deployment.Records.Of(only).AttachedTo = SubjectId.New(Randomness);

        Browser browser = await OpenedAsync("email", Address);

        Answer state = await browser.SendAsync("GET", "/records/access");
        Answer read = await browser.SendAsync("GET", "/records/access/" + attached);

        Assert.Equal([reachable], Reached(state));
        Assert.Equal(
            (StatusCodes.Status404NotFound, ErrorCodes.RecordNotFound.ToString()),
            (read.Status, read.Text("code")));

        Assert.Equal(StatusCodes.Status202Accepted, (await AskedAsync(new Browser(_deployment), "email", alone)).Status);

        _ = await _deployment.CarrySendsAsync();

        Assert.Equal(NoneSent, Assert.Single(_deployment.Mail.Taken, sent => sent.Destination.Value == alone).Body);
    }

    /// <summary>
    /// REC-ACC-001: a record restricted, and one deleting by its own request, are still
    /// reached.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_001_ARestrictedOrSelfDeletingRecordIsReachedAsync()
    {
        SubjectId restricted = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1));
        SubjectId deleting = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1));

        _deployment.Records.Of(restricted).State = PersonRecordState.Restricted;
        (_deployment.Records.Of(deleting).State, _deployment.Records.Of(deleting).DeletingBy) =
            (PersonRecordState.Deleting, DeletionOrigin.Self);

        Browser browser = await OpenedAsync("email", Address);
        Answer state = await browser.SendAsync("GET", "/records/access");

        Assert.Equal(
            new[] { restricted, deleting }.OrderBy(subject => subject.Value),
            Reached(state).OrderBy(subject => subject.Value));
        Assert.Equal(
            ["deleting", "restricted"],
            state.Json().GetProperty("records").EnumerateArray().Select(record => record.GetProperty("state").GetString()).Order());
    }

    /// <summary>
    /// REC-ACC-001 AC4: a session is refused from another browser, after
    /// <c>POST /records/access/end</c>, and once <c>records.access.lifetime</c> has passed
    /// since it opened.
    /// </summary>
    /// <param name="refusal">Which ending is tried.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("another browser")]
    [InlineData("ended")]
    [InlineData("past its lifetime")]
    public async Task REC_ACC_001_AC4_ASessionIsRefusedOnceItHasEndedAsync(string refusal)
    {
        _ = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1));

        Browser browser = await OpenedAsync("email", Address);

        switch (refusal)
        {
            case "another browser":
                browser = new Browser(_deployment);
                _ = await browser.SendAsync("GET", "/records/access");
                break;
            case "ended":
                Assert.Equal(
                    StatusCodes.Status204NoContent,
                    (await browser.SendAsync("POST", "/records/access/end")).Status);
                break;
            case "past its lifetime":
                _deployment.Clock.Advance(TimeSpan.FromMinutes(15));
                break;
        }

        Answer state = await browser.SendAsync("GET", "/records/access");
        Answer referenced = await browser.SendAsync("POST", "/records/access/reference", ("reference", "order-1"));

        Assert.Equal(
            (StatusCodes.Status401Unauthorized, ErrorCodes.SessionExpired.ToString()),
            (state.Status, state.Text("code")));
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), referenced.Text("code"));
    }

    /// <summary>
    /// REC-ACC-001: the session lives <c>records.access.lifetime</c> from its opening,
    /// never extended by use.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_001_TheSessionIsNeverExtendedAsync()
    {
        _ = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1));

        Browser browser = await OpenedAsync("email", Address);
        DateTimeOffset opened = _deployment.Clock.GetUtcNow();

        _deployment.Clock.Advance(TimeSpan.FromMinutes(10));

        Answer state = await browser.SendAsync("GET", "/records/access");

        Assert.Equal(opened + TimeSpan.FromMinutes(15), state.Json().GetProperty("expiresAt").GetDateTimeOffset());
    }

    /// <summary>
    /// REC-ACC-001 AC5: with the phone signal answering <c>risk</c>, no code is sent to the
    /// phone and the ask is answered as any other.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_001_AC5_APhoneTheSignalCallsRiskIsSentNothingAsync()
    {
        await using var risky = new Deployment(
            signals: new PhoneSignalProvider((_, _) => ValueTask.FromResult(PhoneSignal.Risk)));
        OrganizationId shop = Prepared(risky);

        _ = Held(risky, shop, IdentifierKind.Phone, Number, verifiedAgo: TimeSpan.FromDays(1), reference: null);

        Answer held = await AskedAsync(new Browser(risky), "phone", Number);
        Answer unheld = await AskedAsync(new Browser(risky), "phone", "+441632960099");

        _ = await risky.CarrySendsAsync();

        Assert.Equal((StatusCodes.Status202Accepted, unheld.Body), (held.Status, held.Body));
        Assert.Empty(risky.Sms.Taken);
    }

    /// <summary>
    /// REC-ACC-001: a browser holding a live sign-in session is refused with
    /// <c>identity.record.signedin</c>, and nothing is sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_001_ABrowserSignedInIsRefusedAsync()
    {
        _ = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1));

        Browser browser = await Flow.SignedInAsync(_deployment);
        int sent = _deployment.Mail.Taken.Count;

        Answer refused = await browser.SendAsync("POST", "/records/access", ("kind", "email"), ("value", Address));

        _ = await _deployment.CarrySendsAsync();

        Assert.Equal(
            (StatusCodes.Status409Conflict, ErrorCodes.RecordSignedIn.ToString()),
            (refused.Status, refused.Text("code")));
        Assert.Equal(sent, _deployment.Mail.Taken.Count);
    }

    /// <summary>
    /// REC-ACC-001, chapter 09 section 7a: every step of a record access session refuses
    /// a browser holding a live sign-in session with <c>identity.record.signedin</c>, as
    /// the ask does.
    /// </summary>
    /// <param name="method">The step's method.</param>
    /// <param name="path">The step's path.</param>
    /// <param name="member">The member its body names, where it takes one.</param>
    /// <param name="value">The value of that member.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("POST", "/records/access/verify", "code", "123456")]
    [InlineData("GET", "/records/access", null, null)]
    [InlineData("POST", "/records/access/reference", "reference", "booking-1")]
    public async Task REC_ACC_001_EveryStepRefusesABrowserSignedInAsync(
        string method,
        string path,
        string? member,
        string? value)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = member is null
            ? await browser.SendAsync(method, path)
            : await browser.SendAsync(method, path, (member, value!));

        Assert.Equal(
            (StatusCodes.Status409Conflict, ErrorCodes.RecordSignedIn.ToString()),
            (refused.Status, refused.Text("code")));
    }

    /// <summary>
    /// REC-ACC-001, chapter 09 section 7a: a wrong code is refused as invalid whether or
    /// not a record holds the address asked, so the code tells nothing the ask did not;
    /// a browser that asked for no code is answered as for a lapsed one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_001_AWrongCodeIsInvalidWhateverIsHeldAsync()
    {
        _ = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1));

        var held = new Browser(_deployment);
        var unheld = new Browser(_deployment);
        var unasked = new Browser(_deployment);

        _ = await AskedAsync(held, "email", Address);
        _ = await AskedAsync(unheld, "email", "nobody@example.test");
        _ = await unasked.SendAsync("GET", "/records/access");

        Answer wrongHeld = await held.SendAsync("POST", "/records/access/verify", ("code", "000000"));
        Answer wrongUnheld = await unheld.SendAsync("POST", "/records/access/verify", ("code", "000000"));
        Answer none = await unasked.SendAsync("POST", "/records/access/verify", ("code", "000000"));

        Assert.Equal(
            (StatusCodes.Status422UnprocessableEntity, ErrorCodes.CodeInvalid.ToString()),
            (wrongHeld.Status, wrongHeld.Text("code")));
        Assert.Equal(
            (wrongHeld.Status, wrongHeld.Text("code"), wrongHeld.Json().EnumerateObject().Count()),
            (wrongUnheld.Status, wrongUnheld.Text("code"), wrongUnheld.Json().EnumerateObject().Count()));
        Assert.Equal(ErrorCodes.CodeExpired.ToString(), none.Text("code"));
    }

    /// <summary>
    /// REC-ACC-001, AUTH-FACT-004: the code is an authentication code, refused as lapsed
    /// once <c>code.signin.attempts</c> wrong codes have been presented, the right one
    /// included.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_001_TheCodeIsHeldToTheSignInAttemptsAsync()
    {
        _deployment.Configuration.Set(Settings.CodeSigninAttempts, 2);
        _ = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1));

        var browser = new Browser(_deployment);

        _ = await AskedAsync(browser, "email", Address);
        _ = await _deployment.CarrySendsAsync();

        string code = Flow.Code(_deployment, IdentifierKind.Email);
        string wrong = string.Equals(code, "000000", StringComparison.Ordinal) ? "111111" : "000000";

        Answer first = await browser.SendAsync("POST", "/records/access/verify", ("code", wrong));
        Answer second = await browser.SendAsync("POST", "/records/access/verify", ("code", wrong));
        Answer right = await browser.SendAsync("POST", "/records/access/verify", ("code", code));

        Assert.Equal(ErrorCodes.CodeInvalid.ToString(), first.Text("code"));
        Assert.Equal(ErrorCodes.CodeInvalid.ToString(), second.Text("code"));
        Assert.Equal(ErrorCodes.CodeExpired.ToString(), right.Text("code"));
    }

    /// <summary>
    /// REC-ACC-001: a value that is not a well-formed email or phone is refused, and
    /// nothing is sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_001_AMalformedValueIsRefusedAsync()
    {
        Answer refused = await AskedAsync(new Browser(_deployment), "phone", "call me");

        _ = await _deployment.CarrySendsAsync();

        Assert.Equal(
            (StatusCodes.Status422UnprocessableEntity, ErrorCodes.IdentifierInvalid.ToString()),
            (refused.Status, refused.Text("code")));
        Assert.Empty(_deployment.Sms.Taken);
    }

    /// <summary>
    /// REC-ACC-001: the state names each record's organization by its name and gives its
    /// creation.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_001_TheStateNamesEachRecordsOrganizationAsync()
    {
        SubjectId held = Held(IdentifierKind.Email, Address, verifiedAgo: TimeSpan.FromDays(1));

        Browser browser = await OpenedAsync("email", Address);

        JsonElement record = Assert.Single(
            (await browser.SendAsync("GET", "/records/access")).Json().GetProperty("records").EnumerateArray());

        Assert.Equal(held.ToString(), record.GetProperty("subject").GetString());
        Assert.Equal(_shop.ToString(), record.GetProperty("organization").GetString());
        Assert.Equal("Seaside Rentals", record.GetProperty("organizationName").GetString());
        Assert.Equal(_deployment.Records.Of(held).CreatedAt, record.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal("active", record.GetProperty("state").GetString());
    }

    private static OrganizationId Prepared(Deployment deployment)
    {
        Flow.Prepare(deployment);

        foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
        {
            deployment.Templates.Set(
                MessageKind.RecordAccessCode,
                kind,
                Language,
                new MessageTemplate(kind is SendKind.Email ? "code" : null, "{code}"));
        }

        deployment.Templates.Set(
            MessageKind.RecordAccessNone,
            SendKind.Email,
            Language,
            new MessageTemplate("none", NoneSent));

        var shop = OrganizationId.New(deployment.Clock);

        deployment.Organizations.Seed(shop, name: "Seaside Rentals");

        return shop;
    }

    private static SubjectId[] Reached(Answer state) =>
        [
            .. state.Json()
                .GetProperty("records")
                .EnumerateArray()
                .Select(record => new SubjectId(Guid.Parse(record.GetProperty("subject").GetString()!))),
        ];

    // A record collected in the shop, holding a contact verified at a collection some
    // time ago, and a reference where one is named.
    private SubjectId Held(IdentifierKind kind, string value, TimeSpan verifiedAgo, string? reference = null) =>
        Held(_deployment, _shop, kind, value, verifiedAgo, reference);

    private static SubjectId Held(
        Deployment deployment,
        OrganizationId organization,
        IdentifierKind kind,
        string value,
        TimeSpan verifiedAgo,
        string? reference)
    {
        var subject = SubjectId.New(Randomness);
        DateTimeOffset verified = deployment.Clock.GetUtcNow() - verifiedAgo;

        deployment.Records.Hold(
            new HeldRecord(subject, organization, verified)
            {
                Language = Language,
                Contacts = { [kind] = new CollectedContact(kind, value, verified) },
                References = reference is null ? [] : [reference],
            });

        return subject;
    }

    private static async Task<Answer> AskedAsync(Browser browser, string kind, string value)
    {
        _ = await browser.SendAsync("GET", "/records/access");

        return await browser.SendAsync("POST", "/records/access", ("kind", kind), ("value", value));
    }

    // A browser that asked for a code to the value and opened its session with it.
    private async Task<Browser> OpenedAsync(string kind, string value)
    {
        var browser = new Browser(_deployment);

        Assert.Equal(StatusCodes.Status202Accepted, (await AskedAsync(browser, kind, value)).Status);

        _ = await _deployment.CarrySendsAsync();

        Answer opened = await browser.SendAsync(
            "POST",
            "/records/access/verify",
            ("code", Flow.Code(_deployment, kind is "email" ? IdentifierKind.Email : IdentifierKind.Phone)));

        Assert.Equal(StatusCodes.Status200OK, opened.Status);

        return browser;
    }

    private async Task<ThrottleLedgerInMemory> LedgerAsync()
    {
        await using AsyncServiceScope scope = _deployment.Scope();

        return (ThrottleLedgerInMemory)scope.ServiceProvider.GetRequiredService<IThrottleLedger>();
    }
}
