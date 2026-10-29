using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Tests.Records;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Records;

/// <summary>
/// Staff reading and correcting a person record, and reading the records attached to an account
/// (REC-ADM-001, REC-ADM-002, REC-ATT-002, chapter 09 section 8a).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecordAdministrationTests : IAsyncDisposable
{
    private const string Number = "+441632960021";

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    private static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new();

    private readonly OrganizationId _shop;

    private readonly OrganizationId _other;

    private readonly OrganizationId _administration;

    /// <summary>
    /// A deployment with an organization a host keeps callers' data in, another, and the
    /// one that administers it.
    /// </summary>
    public RecordAdministrationTests()
    {
        Flow.Prepare(_deployment);

        _shop = OrganizationId.New(_deployment.Clock);
        _other = OrganizationId.New(_deployment.Clock);
        _administration = OrganizationId.New(_deployment.Clock);
        _deployment.Organizations.Seed(_shop, name: "Corner Shop");
        _deployment.Organizations.Seed(_other);
        _deployment.Administers(_administration);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REC-ADM-001 AC1: a holder of <c>record:read</c> in an organization reads a record
    /// it holds, with its origin and the account it is attached to.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ADM_001_AC1_AHolderReadsARecordItsOrganizationHoldsAsync()
    {
        SubjectId record = Holding(_shop);
        Browser browser = await ReadingAsync(_shop);

        Answer answer = await browser.SendAsync("GET", "/admin/records/" + record.Value);

        Assert.Equal(StatusCodes.Status200OK, answer.Status);
        Assert.Equal(
            (record.Value.ToString(), "Corner Shop", "collection", "active", "en"),
            (answer.Text("subject"), answer.Text("organizationName"), answer.Text("origin"), answer.Text("state"), answer.Text("language")));
        Assert.Equal(
            Number,
            Assert.Single(answer.Json().GetProperty("contacts").EnumerateArray()).GetProperty("value").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, answer.Json().GetProperty("attachedTo").ValueKind);
    }

    /// <summary>
    /// REC-ADM-001 AC1 (D-222): a record another organization holds is answered
    /// <c>identity.record.notfound</c> in bytes identical to a subject no record bears,
    /// save the correlation identifier every answer carries, and the refusal is recorded
    /// once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ADM_001_AC1_ARecordAnotherOrganizationHoldsIsAnsweredAsNoneAsync()
    {
        SubjectId elsewhere = Holding(_other);
        Browser browser = await ReadingAsync(_shop);
        int refused = _deployment.Gate.Refusals.Count;

        Answer beyond = await browser.SendAsync("GET", "/admin/records/" + elsewhere.Value);
        Answer none = await browser.SendAsync("GET", "/admin/records/" + SubjectId.New(Randomness).Value);

        Assert.Equal(
            (StatusCodes.Status404NotFound, ErrorCodes.RecordNotFound.ToString()),
            (beyond.Status, beyond.Text("code")));
        Assert.Equal(none.Status, beyond.Status);
        Assert.Equal(Without(none), Without(beyond));
        Assert.Equal(refused + 1, _deployment.Gate.Refusals.Count);
    }

    /// <summary>
    /// REC-ADM-001 AC1: a holder in the administrative organization reads a record every
    /// organization holds.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ADM_001_AC1_AHolderInTheAdministrativeOrganizationReadsEitherAsync()
    {
        SubjectId shops = Holding(_shop);
        SubjectId others = Holding(_other);
        Browser browser = await ReadingAsync(_administration);

        Answer first = await browser.SendAsync("GET", "/admin/records/" + shops.Value);
        Answer second = await browser.SendAsync("GET", "/admin/records/" + others.Value);

        Assert.Equal((StatusCodes.Status200OK, StatusCodes.Status200OK), (first.Status, second.Status));
        Assert.Equal(others.Value.ToString(), second.Text("subject"));
    }

    /// <summary>
    /// REC-ATT-002 (D-222): the records attached to an account are answered as the
    /// organization asked in holds them, every one in the administrative organization,
    /// and the record's read names the account.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_002_TheAttachedRecordsAreThoseTheOrganizationAskedInHoldsAsync()
    {
        Browser browser = await ReadingAsync(_shop);
        _deployment.Gate.Grant(Staff(), _administration, Permissions.RecordRead);
        var account = SubjectId.New(Randomness);
        _deployment.Accounts.Stands(account, AccountState.Active);
        _deployment.Ties.Tie(account, _shop);
        SubjectId shops = Attached(Holding(_shop), account, Noon);
        _ = Attached(Holding(_other), account, Noon.AddMinutes(1));

        Answer asked = await browser.SendAsync(
            "GET", $"/admin/accounts/{account.Value}/records?organization={_shop.Value}");
        Answer every = await browser.SendAsync("GET", $"/admin/accounts/{account.Value}/records");
        Answer read = await browser.SendAsync("GET", "/admin/records/" + shops.Value);

        Assert.Equal([shops.Value], Subjects(asked));
        Assert.Equal(2, Subjects(every).Length);
        Assert.Equal(account.Value.ToString(), read.Text("attachedTo"));
    }

    /// <summary>
    /// REC-ATT-002, IDN-READ-004: an account outside the reach of the organization asked
    /// in, and a subject no account bears, are answered <c>identity.account.notfound</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_002_AnAccountOutsideTheReachIsAnsweredAsNoneAsync()
    {
        Browser browser = await ReadingAsync(_shop);
        var account = SubjectId.New(Randomness);
        _deployment.Accounts.Stands(account, AccountState.Active);
        _ = Attached(Holding(_shop), account, Noon);

        Answer beyond = await browser.SendAsync(
            "GET", $"/admin/accounts/{account.Value}/records?organization={_shop.Value}");
        Answer none = await browser.SendAsync(
            "GET", $"/admin/accounts/{SubjectId.New(Randomness).Value}/records?organization={_shop.Value}");

        Assert.Equal(
            (StatusCodes.Status404NotFound, ErrorCodes.AccountNotFound.ToString()),
            (beyond.Status, beyond.Text("code")));
        Assert.Equal(Without(none), Without(beyond));
    }

    /// <summary>
    /// REC-ADM-002 AC2: a contact staff correct is held unverified afterwards, a code to
    /// it reaches nothing, and the change is recorded by field with the reason given.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ADM_002_AC2_AContactStaffCorrectIsUnverifiedAndReachesNothingAsync()
    {
        const string corrected = "+441632960022";
        SubjectId record = Holding(_shop);
        Browser browser = await ManagingAsync(_shop);

        Answer answer = await browser.SendAsync(
            "PUT",
            "/admin/records/" + record.Value,
            ("phone", corrected),
            ("reason", "Told us by telephone"));

        CollectedContact held = _deployment.Records.Of(record).Contacts[IdentifierKind.Phone];
        Assert.Equal(StatusCodes.Status204NoContent, answer.Status);
        Assert.Equal((corrected, (DateTimeOffset?)null), (held.Value, held.VerifiedAt));
        Assert.False(await _deployment.Records.ReachableHoldingAsync(IdentifierKind.Phone, [Encoding.UTF8.GetBytes(corrected)], TestContext.Current.CancellationToken));
        Assert.Equal(
            (Staff(), record, "phone", "Told us by telephone"),
            _deployment.RecordAudit.Changed.Select(changed => (changed.Acting, changed.Record, changed.Field, changed.Reason)).Single());
    }

    /// <summary>
    /// REC-ADM-002, AUTHZ-GATE-006 (D-222): a restricted record admits staff's correction,
    /// being how a rectification request is fulfilled.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ADM_002_ARestrictedRecordAdmitsStaffsCorrectionAsync()
    {
        SubjectId record = Holding(_shop);
        _deployment.Records.Of(record).State = PersonRecordState.Restricted;
        Browser browser = await ManagingAsync(_shop);

        Answer answer = await browser.SendAsync(
            "PUT", "/admin/records/" + record.Value, ("name", "Amal Haddad"), ("reason", "Spelling"));

        Assert.Equal(StatusCodes.Status204NoContent, answer.Status);
        Assert.Equal("Amal Haddad", _deployment.Records.Of(record).Name?.Value);
    }

    /// <summary>
    /// REC-ADM-002, 09 section 8a: a correction without a reason is refused
    /// <c>api.request.malformed</c> naming <c>reason</c>, one of a record in its deletion
    /// window <c>identity.record.stateconflict</c>, and one of a record attached to an
    /// account <c>identity.record.followsaccount</c> naming it; none changes the record.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ADM_002_ACorrectionIsRefusedWithoutReasonInItsWindowOrAttachedAsync()
    {
        SubjectId plain = Holding(_shop);
        SubjectId deleting = Holding(_shop);
        _deployment.Records.Of(deleting).State = PersonRecordState.Deleting;
        var account = SubjectId.New(Randomness);
        SubjectId attached = Attached(Holding(_shop), account, Noon);
        Browser browser = await ManagingAsync(_shop);

        Answer unreasoned = await browser.SendAsync("PUT", "/admin/records/" + plain.Value, ("language", "en"));
        Answer windowed = await browser.SendAsync(
            "PUT", "/admin/records/" + deleting.Value, ("language", "en"), ("reason", "Asked"));
        Answer followed = await browser.SendAsync(
            "PUT", "/admin/records/" + attached.Value, ("language", "en"), ("reason", "Asked"));

        Assert.Equal(
            (StatusCodes.Status400BadRequest, ErrorCodes.RequestMalformed.ToString(), "reason"),
            (unreasoned.Status, unreasoned.Text("code"), unreasoned.Json().GetProperty("details").GetProperty("member").GetString()));
        Assert.Equal(
            (StatusCodes.Status409Conflict, ErrorCodes.RecordStateConflict.ToString()),
            (windowed.Status, windowed.Text("code")));
        Assert.Equal(
            (StatusCodes.Status409Conflict, ErrorCodes.RecordFollowsAccount.ToString(), account.Value.ToString()),
            (followed.Status, followed.Text("code"), followed.Json().GetProperty("details").GetProperty("account").GetString()));
        Assert.Empty(_deployment.RecordAudit.Changed);
    }

    private static Guid[] Subjects(Answer answer) =>
        [.. answer.Json().GetProperty("subjects").EnumerateArray().Select(subject => subject.GetGuid())];

    private static string Without(Answer answer) =>
        answer.Body.Replace(answer.Text("correlationId"), string.Empty, StringComparison.Ordinal);

    private SubjectId Staff() => _deployment.Directory.Created[^1].Subject;

    // A signed-in staff member holding record:manage in an organization.
    private async Task<Browser> ManagingAsync(OrganizationId organization)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        _deployment.Gate.Grant(Staff(), organization, Permissions.RecordManage);

        return browser;
    }

    // A signed-in staff member holding record:read in an organization.
    private async Task<Browser> ReadingAsync(OrganizationId organization)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        _deployment.Gate.Grant(Staff(), organization, Permissions.RecordRead);

        return browser;
    }

    private SubjectId Holding(OrganizationId organization)
    {
        var record = new HeldRecord(SubjectId.New(Randomness), organization, Noon)
        {
            Language = "en",
            Contacts = { [IdentifierKind.Phone] = new CollectedContact(IdentifierKind.Phone, Number, Noon) },
        };

        _deployment.Records.Hold(record);

        return record.Subject;
    }

    private SubjectId Attached(SubjectId record, SubjectId account, DateTimeOffset at)
    {
        HeldRecord held = _deployment.Records.Of(record);
        held.AttachedTo = account;
        held.AttachedAt = at;

        return record;
    }
}
