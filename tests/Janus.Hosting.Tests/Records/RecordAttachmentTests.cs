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
/// An account attaching the person records its proved emails and phones hold, through
/// the offer the proof brings (REC-ATT-001, REG-IDENT-004, REG-IDENT-007, REG-SESS-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecordAttachmentTests : IAsyncDisposable
{
    private const string Second = "+441632960022";

    private const string Language = "en";

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    private readonly Deployment _deployment = new();

    private readonly OrganizationId _shop;

    /// <summary>
    /// A deployment able to register a browser, with an organization a host keeps
    /// guests' details in.
    /// </summary>
    public RecordAttachmentTests()
    {
        Flow.Prepare(_deployment);

        _shop = OrganizationId.New(_deployment.Clock);
        _deployment.Organizations.Seed(_shop, name: "Seaside Rentals");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REG-SESS-007 and REC-ATT-001: a registration that proves a phone a recent record
    /// holds brings its session an offer, read as a count and the instant it lapses and
    /// nothing of the record.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_007_ARegistrationProvingAHeldPhoneBringsAnOfferAsync()
    {
        _ = Held(IdentifierKind.Phone, Flow.Number, verifiedAgo: TimeSpan.FromDays(1));

        Browser browser = await Flow.SignedInAsync(_deployment);
        DateTimeOffset proved = _deployment.Clock.GetUtcNow();

        Answer offer = await browser.SendAsync("GET", "/account/records/offer");

        Assert.Equal(StatusCodes.Status200OK, offer.Status);
        Assert.Equal(1, offer.Json().GetProperty("count").GetInt32());
        Assert.Equal(proved + TimeSpan.FromMinutes(15), offer.Json().GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(["count", "expiresAt"], offer.Json().EnumerateObject().Select(member => member.Name));
    }

    /// <summary>
    /// REC-ATT-001 AC1 and REG-IDENT-004: a phone added and verified on the account that
    /// a recent record holds brings an offer of one; attaching it answers one, lists the
    /// record on the account, ends the offer, publishes <c>RecordAttached</c> naming the
    /// record and the account, and audits it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_001_AC1_AnAddedPhoneOffersARecordTheAccountAttachesAsync()
    {
        _deployment.Configuration.Set(Settings.IdentifiersPhoneMax, 2);

        SubjectId record = Held(IdentifierKind.Phone, Second, verifiedAgo: TimeSpan.FromDays(1));

        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId account = _deployment.Directory.Created[^1].Subject;

        Assert.Equal(0, (await browser.SendAsync("GET", "/account/records/offer")).Json().GetProperty("count").GetInt32());

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("POST", "/account/identifiers", ("kind", "phone"), ("value", Second))).Status);

        _ = await _deployment.CarrySendsAsync();

        string staged = Assert.Single(_deployment.Pending.All).Identifier.Value.ToString();

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync(
                "POST",
                "/account/identifiers/" + staged + "/verify",
                ("code", Flow.Code(_deployment, IdentifierKind.Phone)))).Status);
        Assert.Equal(1, (await browser.SendAsync("GET", "/account/records/offer")).Json().GetProperty("count").GetInt32());

        DateTimeOffset attachedAt = _deployment.Clock.GetUtcNow();
        Answer attached = await browser.SendAsync("POST", "/account/records/attach");

        Assert.Equal(StatusCodes.Status200OK, attached.Status);
        Assert.Equal(1, attached.Json().GetProperty("attached").GetInt32());

        JsonElement listed = Assert.Single((await browser.SendAsync("GET", "/account/records")).Json().EnumerateArray());

        Assert.Equal(record.ToString(), listed.GetProperty("subject").GetString());
        Assert.Equal(_shop.ToString(), listed.GetProperty("organization").GetString());
        Assert.Equal("Seaside Rentals", listed.GetProperty("organizationName").GetString());
        Assert.Equal(_deployment.Records.Of(record).CreatedAt, listed.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(attachedAt, listed.GetProperty("attachedAt").GetDateTimeOffset());

        JsonElement ended = (await browser.SendAsync("GET", "/account/records/offer")).Json();

        Assert.Equal(0, ended.GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.Null, ended.GetProperty("expiresAt").ValueKind);
        Assert.Equal(0, (await browser.SendAsync("POST", "/account/records/attach")).Json().GetProperty("attached").GetInt32());
        Assert.Equal(account, _deployment.Records.Of(record).AttachedTo);

        RecordAttached published = Assert.Single(_deployment.Events.Of<RecordAttached>());

        Assert.Equal((record, account), (published.Subject, published.Account));
        Assert.Equal((account, record, _shop, attachedAt), Assert.Single(_deployment.RecordAudit.Attached));
    }

    /// <summary>
    /// REG-IDENT-007 and REC-ATT-001: a replace whose new email a recent record holds
    /// brings the session that completes it an offer of one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_007_AReplaceProvingAHeldEmailBringsAnOfferAsync()
    {
        const string replaced = "replaced@example.test";

        _deployment.Configuration.Set(Settings.IdentifiersEmailMax, 1);

        _ = Held(IdentifierKind.Email, replaced, verifiedAgo: TimeSpan.FromDays(1));

        Browser browser = await Flow.SignedInAsync(_deployment);
        Guid changing = (await _deployment.Identifiers.HeldAsync(
                _deployment.Directory.Created[^1].Subject,
                TestContext.Current.CancellationToken))
            .All
            .Single(held => held.Kind is IdentifierKind.Email)
            .Id
            .Value;

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("PUT", "/account/identifiers/" + changing + "/replace", ("value", replaced))).Status);

        _ = await _deployment.CarrySendsAsync();

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync(
                "POST",
                "/account/identifiers/" + changing + "/verify",
                ("code", Flow.Code(_deployment, IdentifierKind.Email)))).Status);
        Assert.Equal(1, (await browser.SendAsync("GET", "/account/records/offer")).Json().GetProperty("count").GetInt32());
    }

    /// <summary>
    /// REC-ATT-001 AC2: a record collected after the account was registered is offered
    /// once a code sent to the account's phone that holds it is presented.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_001_AC2_ACodeToAVerifiedPhoneFindsALaterRecordAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _ = Held(IdentifierKind.Phone, Flow.Number, verifiedAgo: TimeSpan.FromHours(1));

        JsonElement none = (await browser.SendAsync("GET", "/account/records/offer")).Json();

        Assert.Equal(0, none.GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.Null, none.GetProperty("expiresAt").ValueKind);

        string phone = Assert.Single((await browser.SendAsync("GET", "/account")).Json()
                .GetProperty("identifiers")
                .GetProperty("phones")
                .EnumerateArray())
            .GetProperty("id")
            .GetString()!;

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("POST", "/account/records/find", ("identifier", phone))).Status);

        _ = await _deployment.CarrySendsAsync();

        Answer found = await browser.SendAsync(
            "POST",
            "/account/records/find/verify",
            ("code", Flow.Code(_deployment, IdentifierKind.Phone)));

        Assert.Equal(StatusCodes.Status200OK, found.Status);
        Assert.Equal(1, found.Json().GetProperty("count").GetInt32());
        Assert.Equal(1, (await browser.SendAsync("POST", "/account/records/attach")).Json().GetProperty("attached").GetInt32());
    }

    /// <summary>
    /// REC-ATT-001 AC3: a record whose phone was verified longer ago than
    /// <c>records.proof.age</c> is held but not offered; it is attached only by its
    /// reference, and a reference that matches nothing attaches nothing and is counted
    /// against more than the source.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_001_AC3_AnOlderRecordIsAttachedOnlyByItsReferenceAsync()
    {
        SubjectId older = Held(IdentifierKind.Phone, Flow.Number, verifiedAgo: TimeSpan.FromDays(200), reference: "order-7731");

        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId account = _deployment.Directory.Created[^1].Subject;

        JsonElement offer = (await browser.SendAsync("GET", "/account/records/offer")).Json();

        Assert.Equal(0, offer.GetProperty("count").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, offer.GetProperty("expiresAt").ValueKind);

        Answer missed = await browser.SendAsync("POST", "/account/records/attach/reference", ("reference", "order-0000"));

        Assert.Equal(StatusCodes.Status200OK, missed.Status);
        Assert.Equal(0, missed.Json().GetProperty("attached").GetInt32());
        Assert.Contains((await LedgerAsync()).Failures, failure => failure.Scope is not ThrottleScope.Source);

        Answer matched = await browser.SendAsync("POST", "/account/records/attach/reference", ("reference", "  ORDER-7731 "));

        Assert.Equal(1, matched.Json().GetProperty("attached").GetInt32());
        Assert.Equal(account, _deployment.Records.Of(older).AttachedTo);
    }

    /// <summary>
    /// REC-ATT-001: a record taken down, or already attached, is never offered or
    /// attached.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_001_ARecordOutOfReachIsNeverOfferedAsync()
    {
        SubjectId takenDown = Held(IdentifierKind.Phone, Flow.Number, verifiedAgo: TimeSpan.FromDays(1));
        SubjectId attached = Held(IdentifierKind.Phone, Flow.Number, verifiedAgo: TimeSpan.FromDays(1));

        (_deployment.Records.Of(takenDown).State, _deployment.Records.Of(takenDown).DeletingBy) =
            (PersonRecordState.Deleting, DeletionOrigin.Takedown);
        _deployment.Records.Of(attached).AttachedTo = SubjectId.New(Randomness);

        Browser browser = await Flow.SignedInAsync(_deployment);

        Assert.Equal(0, (await browser.SendAsync("GET", "/account/records/offer")).Json().GetProperty("count").GetInt32());
        Assert.Equal(0, (await browser.SendAsync("POST", "/account/records/attach")).Json().GetProperty("attached").GetInt32());
        Assert.Empty((await browser.SendAsync("GET", "/account/records")).Json().EnumerateArray());
    }

    /// <summary>
    /// REC-ATT-001, chapter 09 section 7a: an attachment by a reference with no offer
    /// standing is refused with <c>identity.record.nooffer</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_001_AReferenceWithNoOfferIsRefusedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync("POST", "/account/records/attach/reference", ("reference", "order-7731"));

        Assert.Equal(
            (StatusCodes.Status409Conflict, ErrorCodes.RecordNoOffer.ToString()),
            (refused.Status, refused.Text("code")));
    }

    /// <summary>
    /// REC-ATT-001, chapter 09 section 7a: a reference that is empty or longer than 64
    /// characters once trimmed is malformed.
    /// </summary>
    /// <param name="reference">The reference given.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("   ")]
    [InlineData("a234567890123456789012345678901234567890123456789012345678901234x")]
    public async Task REC_ATT_001_AReferenceOutOfLengthIsMalformedAsync(string reference)
    {
        _ = Held(IdentifierKind.Phone, Flow.Number, verifiedAgo: TimeSpan.FromDays(1));

        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync("POST", "/account/records/attach/reference", ("reference", reference));

        Assert.Equal(
            (StatusCodes.Status400BadRequest, ErrorCodes.RequestMalformed.ToString()),
            (refused.Status, refused.Text("code")));
    }

    /// <summary>
    /// REC-ATT-001, chapter 09 section 7a: a find names an identifier of the account, and
    /// one that names none is malformed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_001_AFindNamesAnIdentifierOfTheAccountAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer unknown = await browser.SendAsync(
            "POST",
            "/account/records/find",
            ("identifier", Guid.CreateVersion7().ToString()));
        Answer unnamed = await browser.SendAsync("POST", "/account/records/find", "{}");

        Assert.Equal(
            (StatusCodes.Status422UnprocessableEntity, ErrorCodes.IdentifierInvalid.ToString()),
            (unknown.Status, unknown.Text("code")));
        Assert.Equal(
            (StatusCodes.Status400BadRequest, ErrorCodes.RequestMalformed.ToString()),
            (unnamed.Status, unnamed.Text("code")));
    }

    /// <summary>
    /// REC-ATT-001: a restricted account is refused an attachment with
    /// <c>authz.restricted</c>, and nothing is attached.
    /// </summary>
    /// <param name="path">Which attachment.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("/account/records/attach")]
    [InlineData("/account/records/attach/reference")]
    public async Task REC_ATT_001_ARestrictedAccountIsRefusedAnAttachmentAsync(string path)
    {
        SubjectId record = Held(IdentifierKind.Phone, Flow.Number, verifiedAgo: TimeSpan.FromDays(1), reference: "order-7731");

        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Restriction.Restrict(_deployment.Directory.Created[^1].Subject);

        Answer refused = await browser.SendAsync("POST", path, ("reference", "order-7731"));

        Assert.Equal(
            (StatusCodes.Status403Forbidden, ErrorCodes.Restricted.ToString()),
            (refused.Status, refused.Text("code")));
        Assert.Null(_deployment.Records.Of(record).AttachedTo);
    }

    /// <summary>
    /// REC-ATT-001 AC5: an attachment asked of an account that is no longer active when it
    /// is read under its lock is refused with <c>identity.account.stateconflict</c> naming
    /// the state, and attaches nothing.
    /// </summary>
    /// <param name="state">Where the account stands when the attachment is asked.</param>
    /// <param name="written">The state as the refusal names it.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(AccountState.Suspended, "suspended")]
    [InlineData(AccountState.Deleting, "deleting")]
    public async Task REC_ATT_001_AC5_AnAccountNoLongerActiveAttachesNothingAsync(AccountState state, string written)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId record = Held(IdentifierKind.Phone, Flow.Number, verifiedAgo: TimeSpan.FromHours(1));

        string phone = Assert.Single((await browser.SendAsync("GET", "/account")).Json()
                .GetProperty("identifiers")
                .GetProperty("phones")
                .EnumerateArray())
            .GetProperty("id")
            .GetString()!;

        _ = await browser.SendAsync("POST", "/account/records/find", ("identifier", phone));
        _ = await _deployment.CarrySendsAsync();
        _ = await browser.SendAsync("POST", "/account/records/find/verify", ("code", Flow.Code(_deployment, IdentifierKind.Phone)));

        _deployment.Accounts.Stands(_deployment.Directory.Created[^1].Subject, state);

        Answer refused = await browser.SendAsync("POST", "/account/records/attach");

        Assert.Equal(
            (StatusCodes.Status409Conflict, ErrorCodes.AccountStateConflict.ToString()),
            (refused.Status, refused.Text("code")));
        Assert.Equal(written, refused.Json().GetProperty("details").GetProperty("state").GetString());
        Assert.Null(_deployment.Records.Of(record).AttachedTo);
        Assert.Empty(_deployment.Events.Of<RecordAttached>());
    }

    // A record collected in the shop, holding a contact verified at a collection some
    // time ago, and a reference where one is named.
    private SubjectId Held(IdentifierKind kind, string value, TimeSpan verifiedAgo, string? reference = null)
    {
        var subject = SubjectId.New(Randomness);
        DateTimeOffset verified = _deployment.Clock.GetUtcNow() - verifiedAgo;

        _deployment.Records.Hold(
            new HeldRecord(subject, _shop, verified)
            {
                Language = Language,
                Contacts = { [kind] = new CollectedContact(kind, value, verified) },
                References = reference is null ? [] : [reference],
            });

        return subject;
    }

    private async Task<ThrottleLedgerInMemory> LedgerAsync()
    {
        await using AsyncServiceScope scope = _deployment.Scope();

        return (ThrottleLedgerInMemory)scope.ServiceProvider.GetRequiredService<IThrottleLedger>();
    }
}
