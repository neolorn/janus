using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Tests.Records;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Records;

/// <summary>
/// Staff entering a person record for someone who gave their details otherwise than on
/// a host's screen (REC-COL-003, REC-AGE-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecordEntryTests : IAsyncDisposable
{
    private const string Number = "+441632960021";

    private const string Address = "caller@example.test";

    private const string Marketing = "marketing";

    private static readonly DateTimeOffset Published = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new();

    private readonly OrganizationId _shop;

    private Browser? _staff;

    /// <summary>
    /// A deployment with an organization a host keeps callers' data in.
    /// </summary>
    public RecordEntryTests()
    {
        Flow.Prepare(_deployment);

        _shop = OrganizationId.New(_deployment.Clock);
        _deployment.Organizations.Seed(_shop);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REC-COL-003 AC1: a record entered by staff is of origin staff and holds its
    /// contacts unverified, the name and the language given; it answers 201 with its
    /// subject.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_003_AC1_AnEnteredRecordHoldsItsContactsUnverifiedAsync()
    {
        Browser browser = await EnteringAsync();

        Answer answer = await browser.SendAsync(
            "POST",
            "/admin/records",
            ("organization", _shop.Value),
            ("language", "en"),
            ("name", "Rana Haddad"),
            ("email", " Caller@Example.test "),
            ("phone", Number));

        Assert.Equal(201, answer.Status);

        HeldRecord record = _deployment.Records.Of(new SubjectId(Guid.Parse(answer.Text("subject"))));

        Assert.Equal((PersonRecordOrigin.Staff, _shop, "en"), (record.Origin, record.Organization, record.Language));
        Assert.Equal("Rana Haddad", record.Name?.Value);
        Assert.Equal(
            [
                new CollectedContact(IdentifierKind.Email, Address, VerifiedAt: null),
                new CollectedContact(IdentifierKind.Phone, Number, VerifiedAt: null),
            ],
            [.. record.Contacts.Values.OrderBy(contact => contact.Kind)]);
        Assert.Null(record.Age);
        Assert.Empty(record.Documents);
    }

    /// <summary>
    /// REC-COL-003 AC2: a consent entered with the record is recorded with the
    /// mechanism administrator, as the record's own.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_003_AC2_AConsentEnteredWithTheRecordIsRecordedAdministratorAsync()
    {
        _deployment.Documents.Hold(new DocumentVersion("privacy-notice", "1", "en", "The notice.", [], Published));

        SubjectId entered = await EnteredAsync(Entry() with { Consents = [Marketing, Marketing] });

        ConsentRecord consent = Assert.Single(
            await _deployment.Consents.ConsentsAsync(entered, CancellationToken.None));

        Assert.Equal((Marketing, ConsentMechanism.Administrator), (consent.Purpose, consent.Mechanism));
    }

    /// <summary>
    /// REC-COL-003 AC3: an entry by a caller who holds <c>record:create</c> in another
    /// organization alone, or nowhere, is refused 403 <c>authz.denied</c>, and nothing is
    /// written.
    /// </summary>
    /// <param name="elsewhere">Whether the caller holds the permission in another organization.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task REC_COL_003_AC3_AnEntryWithoutTheCreatePermissionInTheOrganizationIsDeniedAsync(bool elsewhere)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        if (elsewhere)
        {
            var other = OrganizationId.New(_deployment.Clock);

            _deployment.Organizations.Seed(other);
            _deployment.Gate.Grant(Staff(), other, Permissions.RecordCreate);
        }

        Answer answer = await browser.SendAsync(
            "POST",
            "/admin/records",
            ("organization", _shop.Value),
            ("language", "en"),
            ("phone", Number));

        Assert.Equal((403, ErrorCodes.Denied.ToString()), (answer.Status, answer.Text("code")));
        Assert.Empty(_deployment.Records.All);
        Assert.Empty(_deployment.RecordAudit.Entered);
    }

    /// <summary>
    /// REC-COL-003: the staff member is the acting identity of
    /// <c>identity.record.entered</c>, the record its subject, filed under the
    /// organization, with whether the age was answered.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_003_TheEntryIsAuditedWithTheStaffMemberActingAsync()
    {
        SubjectId entered = await EnteredAsync(Entry());

        Assert.Equal(
            (Staff(), entered, _shop, false, _deployment.Clock.GetUtcNow()),
            Assert.Single(_deployment.RecordAudit.Entered));
    }

    /// <summary>
    /// REC-AGE-001 AC3: a record entered without a date of birth holds no answer; one
    /// entered with the person's own answer holds the affirmation with the instant, and
    /// the audit says it was answered.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_AGE_001_AC3_AnEntryHoldsTheAgeAnswerOnlyWhereThePersonGaveOneAsync()
    {
        SubjectId unanswered = await EnteredAsync(Entry());
        SubjectId answered = await EnteredAsync(Entry() with { DateOfBirth = new DateOnly(1990, 1, 1) });

        Assert.Null(_deployment.Records.Of(unanswered).Age);
        Assert.Equal(
            new CollectedAge(AdultAffirmed: true, Group: null, DateOfBirth: null, _deployment.Clock.GetUtcNow()),
            _deployment.Records.Of(answered).Age);
        Assert.Equal([false, true], [.. _deployment.RecordAudit.Entered.Select(entry => entry.AgeAnswered)]);
    }

    /// <summary>
    /// REC-COL-003, PRIV-RIGHT-005c: the reference is kept in its folded form alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_003_TheReferenceIsKeptInItsFoldedFormAsync()
    {
        SubjectId entered = await EnteredAsync(Entry() with { Reference = "  ORDER-9902 " });

        Assert.Equal("order-9902", Assert.Single(_deployment.Records.Of(entered).References));
    }

    /// <summary>
    /// 09 section 8a: an entry outside its rules is refused with the code the row names,
    /// and nothing is written or audited.
    /// </summary>
    /// <param name="refusal">Which rule the entry breaks.</param>
    /// <param name="status">The status answered.</param>
    /// <param name="code">The code answered.</param>
    /// <param name="member">The member named, where one is.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("an organization the deployment does not hold", 422, "api.request.invalid", "organization")]
    [InlineData("an organization whose deletion is requested", 422, "api.request.invalid", "organization")]
    [InlineData("a language the deployment does not send in", 422, "api.request.invalid", "language")]
    [InlineData("an email that is not well formed", 422, "identity.identifier.invalid", null)]
    [InlineData("a phone that is not well formed", 422, "identity.identifier.invalid", null)]
    [InlineData("a name outside its rule", 422, "identity.profile.invalid", null)]
    [InlineData("a purpose that does not rest on consent", 422, "privacy.purpose.noconsent", null)]
    [InlineData("a consent whose governing document has no published version", 409, "privacy.notice.unpublished", null)]
    [InlineData("a reference outside its bounds", 400, "api.request.malformed", "reference")]
    [InlineData("no organization", 400, "api.request.malformed", "organization")]
    public async Task REC_COL_003_AnEntryOutsideItsRulesIsRefusedAndKeepsNothingAsync(
        string refusal,
        int status,
        string code,
        string? member)
    {
        Browser browser = await EnteringAsync();
        OrganizationId organization = _shop;
        string language = "en";
        string? name = null;
        string? email = null;
        string phone = Number;
        string[] consents = [];
        string? reference = null;

        switch (refusal)
        {
            case "an organization the deployment does not hold":
                organization = OrganizationId.New(_deployment.Clock);
                break;
            case "an organization whose deletion is requested":
                _deployment.Organizations.Seed(_shop, deletionRequestedAt: _deployment.Clock.GetUtcNow());
                break;
            case "a language the deployment does not send in":
                language = "xx";
                break;
            case "an email that is not well formed":
                email = "not an address";
                break;
            case "a phone that is not well formed":
                phone = "call me";
                break;
            case "a name outside its rule":
                name = new string('n', 300);
                break;
            case "a purpose that does not rest on consent":
                consents = ["performance"];
                break;
            case "a consent whose governing document has no published version":
                consents = ["newsletter"];
                break;
            case "a reference outside its bounds":
                reference = new string('r', 65);
                break;
        }

        Answer answer = refusal is "no organization"
            ? await browser.SendAsync("POST", "/admin/records", ("language", language), ("phone", phone))
            : await browser.SendAsync(
                "POST",
                "/admin/records",
                ("organization", organization.Value),
                ("language", language),
                ("name", name),
                ("email", email),
                ("phone", phone),
                ("consents", consents),
                ("reference", reference));

        Assert.Equal((status, code), (answer.Status, answer.Text("code")));

        if (member is not null)
        {
            Assert.Equal(member, answer.Json().GetProperty("details").GetProperty("member").GetString());
        }

        Assert.Empty(_deployment.Records.All);
        Assert.Empty(_deployment.RecordAudit.Entered);
    }

    private static PersonRecordEntry Entry() =>
        new(
            OrganizationId.New(TimeProvider.System),
            "en",
            Name: null,
            Email: null,
            Number,
            Consents: [],
            DateOfBirth: null,
            Reference: null);

    private SubjectId Staff() => _deployment.Directory.Created[^1].Subject;

    // A signed-in staff member holding record:create in the shop, signed in once.
    private async Task<Browser> EnteringAsync()
    {
        if (_staff is null)
        {
            _staff = await Flow.SignedInAsync(_deployment);
            _deployment.Gate.Grant(Staff(), _shop, Permissions.RecordCreate);
        }

        return _staff;
    }

    // The entry made through the operation by the staff member, in the shop.
    private async Task<SubjectId> EnteredAsync(PersonRecordEntry entry)
    {
        _ = await EnteringAsync();

        await using AsyncServiceScope scope = _deployment.Scope();

        Result<SubjectId> entered = await scope.ServiceProvider
            .GetRequiredService<IPersonRecords>()
            .EnterAsync(AccessContext.Of(Staff()), entry with { Organization = _shop }, CancellationToken.None);

        return entered.Match(subject => subject, error => throw new InvalidOperationException(error.Code.ToString()));
    }
}
