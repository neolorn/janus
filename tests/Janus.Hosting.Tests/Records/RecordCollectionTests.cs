using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Tests.Records;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Records;

/// <summary>
/// The collection of a person record from a host's own screen: the session the browser
/// holds admits it, and the record it verified is written or reused (REC-COL-002,
/// REC-AGE-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecordCollectionTests : IAsyncDisposable
{
    private const string Checkout = "/shop/checkout";

    private const string Number = "+441632960011";

    private const string Address = "guest@example.test";

    private const string Marketing = "marketing";

    private static readonly DateTimeOffset Published = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    private readonly Deployment _deployment;

    private readonly OrganizationId _shop;

    private RecordCollection _collection;

    private Result<CollectedRecord> _outcome;

    /// <summary>
    /// A deployment whose host collects at its checkout, with an organization it keeps
    /// guests' data in and a privacy notice published.
    /// </summary>
    public RecordCollectionTests()
    {
        _deployment = new Deployment(host: Map);
        Flow.Prepare(_deployment);

        _shop = OrganizationId.New(_deployment.Clock);
        _deployment.Organizations.Seed(_shop);
        _deployment.Documents.Hold(new DocumentVersion("privacy-notice", "1", "en", "The notice.", [], Published));

        _collection = Collection();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REC-COL-002 AC1: a collection whose session verified a phone writes a record of
    /// origin collection holding the phone verified now, the notice and terms versions
    /// with the instant, and one consent per ticked purpose recorded collection.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_AC1_ACollectionWritesTheRecordItsSessionVerifiedAsync()
    {
        Browser browser = await VerifiedAsync(IdentifierKind.Phone, Number);
        _collection = Collection() with { TermsVersion = "7", Consents = [Marketing] };

        CollectedRecord collected = await CollectedAsync(browser);

        HeldRecord record = _deployment.Records.Of(collected.Subject);
        DateTimeOffset now = _deployment.Clock.GetUtcNow();

        Assert.True(collected.Created);
        Assert.Equal(_shop, record.Organization);
        Assert.Equal(new CollectedContact(IdentifierKind.Phone, Number, now), Assert.Single(record.Contacts.Values));
        Assert.Equal(("1", "7", now), Assert.Single(record.Documents));

        ConsentRecord consent = Assert.Single(
            await _deployment.Consents.ConsentsAsync(collected.Subject, CancellationToken.None));

        Assert.Equal((Marketing, ConsentMechanism.Collection), (consent.Purpose, consent.Mechanism));
        Assert.Equal((collected.Subject, _shop, true), Single(_deployment.RecordAudit.Collected));
    }

    /// <summary>
    /// REC-COL-002 AC1: a second collection with the same phone in the same organization
    /// within the proof's age reuses the record; one after it writes a new one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_AC1_ASecondCollectionWithinTheProofsAgeReusesTheRecordAsync()
    {
        CollectedRecord first = await CollectedAsync(await VerifiedAsync(IdentifierKind.Phone, Number));

        _deployment.Clock.Advance(TimeSpan.FromDays(89));

        CollectedRecord second = await CollectedAsync(await VerifiedAsync(IdentifierKind.Phone, Number));

        _deployment.Clock.Advance(TimeSpan.FromDays(91));

        CollectedRecord third = await CollectedAsync(await VerifiedAsync(IdentifierKind.Phone, Number));

        Assert.Equal((first.Subject, false), (second.Subject, second.Created));
        Assert.True(third.Created);
        Assert.NotEqual(first.Subject, third.Subject);
        Assert.Equal(
            _deployment.Clock.GetUtcNow().AddDays(-91),
            _deployment.Records.Of(first.Subject).Contacts[IdentifierKind.Phone].VerifiedAt);
        Assert.Equal([true, false, true], [.. _deployment.RecordAudit.Collected.Select(entry => entry.Created)]);
    }

    /// <summary>
    /// REC-COL-002 AC1: a collection that verified the record's phone and another email
    /// than the one the record holds writes a new record.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_AC1_AnotherEmailThanTheRecordHoldsWritesANewRecordAsync()
    {
        CollectedRecord first = await CollectedAsync(
            await VerifiedAsync((IdentifierKind.Phone, Number), (IdentifierKind.Email, Address)));

        _deployment.Clock.Advance(TimeSpan.FromMinutes(1));

        CollectedRecord other = await CollectedAsync(
            await VerifiedAsync((IdentifierKind.Phone, Number), (IdentifierKind.Email, "another@example.test")));

        _deployment.Clock.Advance(TimeSpan.FromMinutes(1));

        CollectedRecord same = await CollectedAsync(await VerifiedAsync(IdentifierKind.Phone, Number));

        Assert.True(other.Created);
        Assert.NotEqual(first.Subject, other.Subject);
        Assert.False(same.Created);
        Assert.Equal(other.Subject, same.Subject);
    }

    /// <summary>
    /// REC-COL-002: a record attached to an account, one not active, and one of another
    /// organization are never reused.
    /// </summary>
    /// <param name="reach">What keeps the record from being reused.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("attached")]
    [InlineData("restricted")]
    [InlineData("organization")]
    public async Task REC_COL_002_ARecordOutsideTheCollectionsReachIsNotReusedAsync(string reach)
    {
        CollectedRecord first = await CollectedAsync(await VerifiedAsync(IdentifierKind.Phone, Number));
        HeldRecord held = _deployment.Records.Of(first.Subject);
        var elsewhere = OrganizationId.New(_deployment.Clock);

        _deployment.Organizations.Seed(elsewhere);

        switch (reach)
        {
            case "attached":
                held.AttachedTo = SubjectId.New(Randomness);
                break;
            case "restricted":
                held.State = PersonRecordState.Restricted;
                break;
            default:
                _collection = Collection() with { Organization = elsewhere };
                break;
        }

        CollectedRecord second = await CollectedAsync(await VerifiedAsync(IdentifierKind.Phone, Number));

        Assert.True(second.Created);
        Assert.NotEqual(first.Subject, second.Subject);
    }

    /// <summary>
    /// REC-COL-002 AC2: a collection from a request whose browser holds no collection
    /// session is refused and writes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_AC2_ABrowserHoldingNoSessionIsRefusedAndNothingIsWrittenAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/records/collection");

        Error refused = await RefusedAsync(browser);

        Assert.Equal(ErrorCodes.CollectionExpired, refused.Code);
        Assert.Empty(_deployment.Records.All);
        Assert.Empty(_deployment.RecordAudit.Collected);
    }

    /// <summary>
    /// REC-COL-002 AC2: a browser signed in holds no collection session, and a session
    /// spent, abandoned or expired admits no collection.
    /// </summary>
    /// <param name="after">What happened to the session first.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("spent")]
    [InlineData("abandoned")]
    [InlineData("expired")]
    public async Task REC_COL_002_AC2_ASessionSpentAbandonedOrExpiredAdmitsNoCollectionAsync(string after)
    {
        Browser browser = await VerifiedAsync(IdentifierKind.Phone, Number);

        switch (after)
        {
            case "spent":
                _ = await CollectedAsync(browser);
                break;
            case "abandoned":
                _ = await browser.SendAsync("POST", "/records/collection/abandon");
                break;
            default:
                // The browser's first contact lapses with the session it carried, so it
                // comes back with a new one, which carries none.
                _deployment.Clock.Advance(TimeSpan.FromHours(1));
                _ = await browser.SendAsync("GET", "/records/collection");
                break;
        }

        Error refused = await RefusedAsync(browser);

        Assert.Equal(ErrorCodes.CollectionExpired, refused.Code);
        Assert.True(_deployment.Records.All.Count <= 1);
    }

    /// <summary>
    /// REC-COL-002 AC2: a browser holding a sign-in session holds no collection session.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_AC2_ABrowserSignedInIsRefusedAsAnExpiredCollectionAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Assert.Equal(ErrorCodes.CollectionExpired, (await RefusedAsync(browser)).Code);
    }

    /// <summary>
    /// REC-COL-002: called while no browser's request is served, the collection is a
    /// fault of the calling code.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_OutsideABrowsersRequestTheCollectionIsAFaultAsync()
    {
        await using AsyncServiceScope scope = _deployment.Scope();

        IRecordCollection records = scope.ServiceProvider.GetRequiredService<IRecordCollection>();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await records.CollectAsync(_collection, CancellationToken.None));
    }

    /// <summary>
    /// REC-COL-002 AC3: a notice version a later publication superseded is refused
    /// naming <c>noticeVersion</c>, and before any is published the collection is
    /// refused as unpublished.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_AC3_ASupersededNoticeVersionIsRefusedAsync()
    {
        Browser browser = await VerifiedAsync(IdentifierKind.Phone, Number);

        _deployment.Documents.Hold(new DocumentVersion("privacy-notice", "2", "en", "The notice again.", [], Published.AddDays(1)));

        Error refused = await RefusedAsync(browser);

        Assert.Equal(ErrorCodes.ConsentSuperseded, refused.Code);
        Assert.Equal("noticeVersion", refused.Details["member"].GetString());
        Assert.Empty(_deployment.Records.All);

        _collection = Collection() with { NoticeVersion = "2" };

        Assert.True((await CollectedAsync(browser)).Created);
    }

    /// <summary>
    /// REC-COL-002: before any version of the privacy notice is published, a collection
    /// is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_NoNoticePublishedRefusesTheCollectionAsync()
    {
        await using var bare = new Deployment(host: Map);
        Flow.Prepare(bare);
        bare.Organizations.Seed(_shop);

        Browser browser = await VerifiedAsync(bare, (IdentifierKind.Phone, Number));

        Error refused = await RefusedAsync(browser);

        Assert.Equal(ErrorCodes.NoticeUnpublished, refused.Code);
        Assert.Empty(bare.Records.All);
    }

    /// <summary>
    /// REC-COL-002: a staged contact unverified, an age the session asked unanswered, or
    /// no contact at all is refused as incomplete, and nothing is written.
    /// </summary>
    /// <param name="missing">What the session lacks.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("unverified")]
    [InlineData("age")]
    [InlineData("contact")]
    public async Task REC_COL_002_AnIncompleteSessionIsRefusedAsync(string missing)
    {
        Browser browser = await BegunAsync(_deployment, age: missing is "age");

        if (missing is "unverified")
        {
            _ = await browser.SendAsync("PUT", "/records/collection/phone", ("value", Number));
        }

        Error refused = await RefusedAsync(browser);

        Assert.Equal(ErrorCodes.CollectionIncomplete, refused.Code);
        Assert.Empty(_deployment.Records.All);
        Assert.NotEmpty(_deployment.Collections.All);
    }

    /// <summary>
    /// REC-COL-002: a contact the screen took and did not verify is enough where the
    /// session verified none, and is kept unverified.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_AContactTheScreenTookIsKeptUnverifiedAsync()
    {
        Browser browser = await BegunAsync(_deployment, age: false);
        _collection = Collection() with
        {
            Contacts = new Dictionary<IdentifierKind, string> { [IdentifierKind.Email] = " Guest@Example.test " },
        };

        CollectedRecord collected = await CollectedAsync(browser);

        Assert.Equal(
            new CollectedContact(IdentifierKind.Email, Address, VerifiedAt: null),
            Assert.Single(_deployment.Records.Of(collected.Subject).Contacts.Values));
    }

    /// <summary>
    /// REC-COL-002: on a record reused, a contact the screen took is kept only where the
    /// record holds none of its kind, and a contact the session verified is gained.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_AReusedRecordGainsOnlyAContactOfAKindItHeldNoneOfAsync()
    {
        _collection = Collection() with
        {
            Contacts = new Dictionary<IdentifierKind, string> { [IdentifierKind.Email] = Address },
        };

        CollectedRecord first = await CollectedAsync(await VerifiedAsync(IdentifierKind.Phone, Number));

        _collection = Collection() with
        {
            Contacts = new Dictionary<IdentifierKind, string> { [IdentifierKind.Email] = "another@example.test" },
        };

        CollectedRecord second = await CollectedAsync(await VerifiedAsync(IdentifierKind.Phone, Number));

        Assert.Equal(first.Subject, second.Subject);
        Assert.Equal(Address, _deployment.Records.Of(first.Subject).Contacts[IdentifierKind.Email].Value);
    }

    /// <summary>
    /// REC-COL-002: what the screen took outside its bounds is refused naming the member,
    /// and nothing is written.
    /// </summary>
    /// <param name="member">The member out of bounds.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("organization")]
    [InlineData("language")]
    [InlineData("reference")]
    [InlineData("contacts")]
    public async Task REC_COL_002_AMemberOutsideItsBoundsIsRefusedNamingItAsync(string member)
    {
        Browser browser = await VerifiedAsync(IdentifierKind.Phone, Number);
        var deleting = OrganizationId.New(_deployment.Clock);

        _deployment.Organizations.Seed(deleting, deletionRequestedAt: Published);

        _collection = member switch
        {
            "organization" => Collection() with { Organization = deleting },
            "language" => Collection() with { Language = "xx" },
            "reference" => Collection() with { Reference = new string('7', 65) },
            _ => Collection() with
            {
                Contacts = new Dictionary<IdentifierKind, string> { [IdentifierKind.Phone] = "+441632960099" },
            },
        };

        Error refused = await RefusedAsync(browser);

        Assert.Equal(ErrorCodes.RequestInvalid, refused.Code);
        Assert.Equal(member, refused.Details["member"].GetString());
        Assert.Empty(_deployment.Records.All);
    }

    /// <summary>
    /// REC-COL-002: a contact the screen took that is not a well-formed value of its
    /// kind, or of a kind a record does not hold, is refused naming the contacts.
    /// </summary>
    /// <param name="kind">The contact's kind.</param>
    /// <param name="value">Its value.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(IdentifierKind.Email, "not an address")]
    [InlineData(IdentifierKind.Username, "guest")]
    public async Task REC_COL_002_AContactNotWellFormedIsRefusedAsync(IdentifierKind kind, string value)
    {
        Browser browser = await VerifiedAsync(IdentifierKind.Phone, Number);
        _collection = Collection() with { Contacts = new Dictionary<IdentifierKind, string> { [kind] = value } };

        Error refused = await RefusedAsync(browser);

        Assert.Equal((ErrorCodes.RequestInvalid, "contacts"), (refused.Code, refused.Details["member"].GetString()));
    }

    /// <summary>
    /// REC-COL-002: a name outside its rule is refused as a profile field is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_ANameOutsideItsRuleIsRefusedAsync()
    {
        Browser browser = await VerifiedAsync(IdentifierKind.Phone, Number);
        _collection = Collection() with { Name = new string('n', 65) };

        Assert.Equal(ErrorCodes.ProfileInvalid, (await RefusedAsync(browser)).Code);
        Assert.Empty(_deployment.Records.All);
    }

    /// <summary>
    /// REC-COL-002: a purpose that does not rest on consent is refused, and nothing of
    /// the collection is kept.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_APurposeThatDoesNotRestOnConsentIsRefusedAsync()
    {
        Browser browser = await VerifiedAsync(IdentifierKind.Phone, Number);
        _collection = Collection() with { Consents = ["performance"] };

        Error refused = await RefusedAsync(browser);

        Assert.Equal(ErrorCodes.PurposeNoConsent, refused.Code);
        Assert.Empty(_deployment.Records.All);
        Assert.Single(_deployment.Collections.All);
        Assert.Empty(_deployment.RecordAudit.Collected);
    }

    /// <summary>
    /// REC-COL-002, PRIV-RIGHT-005c: the reference is kept in its folded form, from which
    /// its keyed fingerprint is taken, and in no other.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_AC4_TheReferenceIsKeptInItsFoldedFormAsync()
    {
        Browser browser = await VerifiedAsync(IdentifierKind.Phone, Number);
        _collection = Collection() with { Reference = "  ORDER-4471 " };

        CollectedRecord collected = await CollectedAsync(browser);

        Assert.Equal("order-4471", Assert.Single(_deployment.Records.Of(collected.Subject).References));
    }

    /// <summary>
    /// REC-COL-002, REC-AGE-001: the record takes the age answer its session staged,
    /// and a record reused keeps the answer it holds.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_AGE_001_TheRecordCarriesTheAgeAnswerFromItsCollectionAsync()
    {
        Browser asked = await BegunAsync(_deployment, age: true);

        _ = await asked.SendAsync("PUT", "/records/collection/age", ("dateOfBirth", "1990-01-01"));
        await VerifyAsync(_deployment, asked, IdentifierKind.Phone, Number);

        DateTimeOffset answered = _deployment.Clock.GetUtcNow();
        CollectedRecord first = await CollectedAsync(asked);

        _deployment.Clock.Advance(TimeSpan.FromDays(1));

        Browser again = await BegunAsync(_deployment, age: true);

        _ = await again.SendAsync("PUT", "/records/collection/age", ("dateOfBirth", "1991-01-01"));
        await VerifyAsync(_deployment, again, IdentifierKind.Phone, Number);

        CollectedRecord second = await CollectedAsync(again);
        CollectedAge? age = _deployment.Records.Of(first.Subject).Age;

        Assert.Equal(first.Subject, second.Subject);
        Assert.Equal(answered, age?.AnsweredAt);
        Assert.True(age?.AdultAffirmed);
    }

    /// <summary>
    /// REC-COL-002 AC5: the values a collection verified are locked before any record is
    /// judged, so collections of one value are decided one after another.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_002_AC5_TheVerifiedValuesAreLockedBeforeAnyRecordIsJudgedAsync()
    {
        _ = await CollectedAsync(await VerifiedAsync((IdentifierKind.Phone, Number), (IdentifierKind.Email, Address)));

        Assert.Equal(
            [(IdentifierKind.Phone, Number), (IdentifierKind.Email, Address)],
            [.. _deployment.Records.Locked.OrderByDescending(value => value.Kind)]);
    }

    private static (SubjectId Record, OrganizationId Organization, bool Created) Single(
        IReadOnlyList<(SubjectId Record, OrganizationId Organization, bool Created, DateTimeOffset At)> entries)
    {
        (SubjectId record, OrganizationId organization, bool created, _) = Assert.Single(entries);

        return (record, organization, created);
    }

    private static async Task<Browser> BegunAsync(Deployment deployment, bool age)
    {
        var browser = new Browser(deployment);

        _ = await browser.SendAsync("GET", "/records/collection");
        Assert.Equal(
            StatusCodes.Status201Created,
            (await browser.SendAsync("POST", "/records/collection", ("age", age))).Status);

        return browser;
    }

    private static async Task VerifyAsync(Deployment deployment, Browser browser, IdentifierKind kind, string value)
    {
        string path = kind is IdentifierKind.Email ? "/records/collection/email" : "/records/collection/phone";

        Assert.Equal(StatusCodes.Status202Accepted, (await browser.SendAsync("PUT", path, ("value", value))).Status);
        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync(
                "POST",
                "/records/collection/verify",
                ("kind", kind is IdentifierKind.Email ? "email" : "phone"),
                ("code", Flow.Code(deployment, kind)))).Status);
    }

    private static async Task<Browser> VerifiedAsync(Deployment deployment, params (IdentifierKind Kind, string Value)[] contacts)
    {
        Browser browser = await BegunAsync(deployment, age: false);

        foreach ((IdentifierKind kind, string value) in contacts)
        {
            await VerifyAsync(deployment, browser, kind, value);
        }

        return browser;
    }

    private Task<Browser> VerifiedAsync(IdentifierKind kind, string value) =>
        VerifiedAsync(_deployment, (kind, value));

    private Task<Browser> VerifiedAsync(params (IdentifierKind Kind, string Value)[] contacts) =>
        VerifiedAsync(_deployment, contacts);

    private RecordCollection Collection() =>
        new(_shop, "en", Name: null, "1", TermsVersion: null, [], Contacts: null, Reference: null);

    private async Task<CollectedRecord> CollectedAsync(Browser browser)
    {
        Assert.Equal(StatusCodes.Status204NoContent, (await browser.SendAsync("POST", Checkout)).Status);

        return _outcome.Match(collected => collected, error => throw new InvalidOperationException("Refused: " + error.Code + "."));
    }

    private async Task<Error> RefusedAsync(Browser browser)
    {
        Assert.Equal(StatusCodes.Status204NoContent, (await browser.SendAsync("POST", Checkout)).Status);

        return _outcome.Match(collected => throw new InvalidOperationException("Collected " + collected.Subject + "."), error => error);
    }

    // The host's checkout, which collects the guest's details as its own screen took them.
    private void Map(IEndpointRouteBuilder endpoints) =>
        _ = endpoints.MapPost(Checkout, async (IRecordCollection records, CancellationToken cancellationToken) =>
        {
            _outcome = await records.CollectAsync(_collection, cancellationToken);

            return Results.NoContent();
        }).AllowNoSession();
}
