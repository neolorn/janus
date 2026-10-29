using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Tests.Records;
using Janus.Core;
using Xunit;

namespace Janus.Hosting.Tests.Records;

/// <summary>
/// Staff finding the person records an organization holds by a contact or a reference
/// (REC-ADM-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecordFindTests : IAsyncDisposable
{
    private const string Number = "+441632960021";

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    private static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new();

    private readonly OrganizationId _shop;

    private readonly OrganizationId _other;

    /// <summary>
    /// A deployment with an organization a host keeps callers' data in, and another.
    /// </summary>
    public RecordFindTests()
    {
        Flow.Prepare(_deployment);

        _shop = OrganizationId.New(_deployment.Clock);
        _other = OrganizationId.New(_deployment.Clock);
        _deployment.Organizations.Seed(_shop);
        _deployment.Organizations.Seed(_other);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REC-ADM-001 AC2: finding by a phone written with Arabic-Indic digits answers the
    /// records of the organization holding its E.164 form, verified or not, in the order
    /// of their subjects, and none another organization holds.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ADM_001_AC2_FindingByAPhoneInArabicIndicDigitsAnswersTheRecordHoldingItAsync()
    {
        SubjectId collected = Holding(_shop, IdentifierKind.Phone, Number, Noon);
        SubjectId entered = Holding(_shop, IdentifierKind.Phone, Number, verifiedAt: null);
        _ = Holding(_other, IdentifierKind.Phone, Number, Noon);
        _ = Holding(_shop, IdentifierKind.Phone, "+441632960022", Noon);

        Answer answer = await (await FindingAsync()).SendAsync(
            "POST",
            "/admin/records/find",
            ("organization", _shop.Value),
            ("kind", "phone"),
            ("value", "+٤٤١٦٣٢٩٦٠٠٢١"));

        Assert.Equal(200, answer.Status);
        Assert.Equal(
            [.. new[] { collected, entered }.Select(subject => subject.Value).Order()],
            Subjects(answer));
    }

    /// <summary>
    /// REC-ADM-001 AC2, IDN-ACCT-004: finding by an email in another case answers the
    /// record holding its canonical form.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ADM_001_AC2_FindingByAnEmailReadsItInItsCanonicalFormAsync()
    {
        SubjectId held = Holding(_shop, IdentifierKind.Email, "caller@example.test", verifiedAt: null);

        Answer answer = await (await FindingAsync()).SendAsync(
            "POST",
            "/admin/records/find",
            ("organization", _shop.Value),
            ("kind", "email"),
            ("value", " Caller@Example.TEST "));

        Assert.Equal([held.Value], Subjects(answer));
    }

    /// <summary>
    /// REC-ADM-001 AC2: finding by a reference answers the records of the organization
    /// holding it, read in its folded form.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ADM_001_AC2_FindingByAReferenceAnswersTheRecordsHoldingItAsync()
    {
        SubjectId first = Referenced(_shop, "order-9902");
        SubjectId second = Referenced(_shop, "order-9902");
        _ = Referenced(_shop, "order-9903");
        _ = Referenced(_other, "order-9902");

        Answer answer = await (await FindingAsync()).SendAsync(
            "POST",
            "/admin/records/find",
            ("organization", _shop.Value),
            ("reference", "  ORDER-9902 "));

        Assert.Equal(200, answer.Status);
        Assert.Equal([.. new[] { first, second }.Select(subject => subject.Value).Order()], Subjects(answer));
    }

    /// <summary>
    /// REC-ADM-001: a value no record of the organization holds, one not well formed and
    /// an erased record's alike answer an empty list.
    /// </summary>
    /// <param name="value">The phone entered.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("+441632960029")]
    [InlineData("not a phone")]
    [InlineData(Number)]
    public async Task REC_ADM_001_AValueNoRecordHoldsAnswersAnEmptyListAsync(string value)
    {
        SubjectId erased = Holding(_shop, IdentifierKind.Phone, Number, Noon);
        _deployment.Records.Of(erased).State = PersonRecordState.Deleted;

        Answer answer = await (await FindingAsync()).SendAsync(
            "POST",
            "/admin/records/find",
            ("organization", _shop.Value),
            ("kind", "phone"),
            ("value", value));

        Assert.Equal(200, answer.Status);
        Assert.Empty(Subjects(answer));
    }

    /// <summary>
    /// REC-ADM-001: a caller who holds <c>record:read</c> in another organization alone,
    /// or nowhere, is refused 403 <c>authz.denied</c>.
    /// </summary>
    /// <param name="elsewhere">Whether the caller holds the permission in another organization.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task REC_ADM_001_AFindingWithoutTheReadPermissionInTheOrganizationIsDeniedAsync(bool elsewhere)
    {
        _ = Holding(_shop, IdentifierKind.Phone, Number, Noon);
        Browser browser = await Flow.SignedInAsync(_deployment);

        if (elsewhere)
        {
            _deployment.Gate.Grant(Staff(), _other, Permissions.RecordRead);
        }

        Answer answer = await browser.SendAsync(
            "POST",
            "/admin/records/find",
            ("organization", _shop.Value),
            ("kind", "phone"),
            ("value", Number));

        Assert.Equal((403, ErrorCodes.Denied.ToString()), (answer.Status, answer.Text("code")));
    }

    /// <summary>
    /// REC-ADM-001 (D-222): a finding asked in the administrative organization answers
    /// the records every organization holds that hold the value.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ADM_001_AFindingInTheAdministrativeOrganizationAnswersEveryOrganizationsRecordsAsync()
    {
        var administration = OrganizationId.New(_deployment.Clock);
        _deployment.Administers(administration);
        SubjectId shops = Holding(_shop, IdentifierKind.Phone, Number, Noon);
        SubjectId others = Holding(_other, IdentifierKind.Phone, Number, verifiedAt: null);
        SubjectId referenced = Referenced(_other, "order-9902");
        Browser browser = await Flow.SignedInAsync(_deployment);
        _deployment.Gate.Grant(Staff(), administration, Permissions.RecordRead);

        Answer byPhone = await browser.SendAsync(
            "POST",
            "/admin/records/find",
            ("organization", administration.Value),
            ("kind", "phone"),
            ("value", Number));
        Answer byReference = await browser.SendAsync(
            "POST",
            "/admin/records/find",
            ("organization", administration.Value),
            ("reference", "order-9902"));

        Assert.Equal(
            [.. new[] { shops, others }.Select(subject => subject.Value).Order()],
            Subjects(byPhone));
        Assert.Equal([referenced.Value], Subjects(byReference));
    }

    /// <summary>
    /// REC-ADM-001 (D-222): an organization the deployment never held, and one it erased,
    /// are answered 404 <c>identity.organization.notfound</c>.
    /// </summary>
    /// <param name="erased">Whether the organization named was held and erased.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task REC_ADM_001_AFindingInAnOrganizationTheDeploymentDoesNotHoldIsNotFoundAsync(bool erased)
    {
        var named = OrganizationId.New(_deployment.Clock);

        if (erased)
        {
            _deployment.Organizations.Seed(named, erasedAt: Noon);
        }

        Browser browser = await Flow.SignedInAsync(_deployment);
        _deployment.Gate.Grant(Staff(), named, Permissions.RecordRead);

        Answer answer = await browser.SendAsync(
            "POST",
            "/admin/records/find",
            ("organization", named.Value),
            ("kind", "phone"),
            ("value", Number));

        Assert.Equal((404, ErrorCodes.OrganizationNotFound.ToString()), (answer.Status, answer.Text("code")));
    }

    /// <summary>
    /// 09 section 8a: a body of neither shape is refused 400 <c>api.request.malformed</c>
    /// naming the member out of its rule.
    /// </summary>
    /// <param name="shape">How the body is out of its rule.</param>
    /// <param name="member">The member named.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("no organization", "organization")]
    [InlineData("a reference beside a contact", "reference")]
    [InlineData("no kind", "kind")]
    [InlineData("no value", "value")]
    public async Task REC_ADM_001_ABodyOfNeitherShapeIsMalformedAsync(string shape, string member)
    {
        Browser browser = await FindingAsync();

        Answer answer = shape switch
        {
            "no organization" => await browser.SendAsync(
                "POST",
                "/admin/records/find",
                ("kind", "phone"),
                ("value", Number)),
            "a reference beside a contact" => await browser.SendAsync(
                "POST",
                "/admin/records/find",
                ("organization", _shop.Value),
                ("kind", "phone"),
                ("value", Number),
                ("reference", "order-9902")),
            "no kind" => await browser.SendAsync(
                "POST",
                "/admin/records/find",
                ("organization", _shop.Value),
                ("value", Number)),
            _ => await browser.SendAsync(
                "POST",
                "/admin/records/find",
                ("organization", _shop.Value),
                ("kind", "phone")),
        };

        Assert.Equal((400, ErrorCodes.RequestMalformed.ToString()), (answer.Status, answer.Text("code")));
        Assert.Equal(member, answer.Json().GetProperty("details").GetProperty("member").GetString());
    }

    private static Guid[] Subjects(Answer answer) =>
        [.. answer.Json().GetProperty("subjects").EnumerateArray().Select(subject => subject.GetGuid())];

    private SubjectId Staff() => _deployment.Directory.Created[^1].Subject;

    // A signed-in staff member holding record:read in the shop.
    private async Task<Browser> FindingAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        _deployment.Gate.Grant(Staff(), _shop, Permissions.RecordRead);

        return browser;
    }

    private SubjectId Holding(OrganizationId organization, IdentifierKind kind, string value, DateTimeOffset? verifiedAt)
    {
        var record = new HeldRecord(SubjectId.New(Randomness), organization, Noon)
        {
            Contacts = { [kind] = new CollectedContact(kind, value, verifiedAt) },
        };

        _deployment.Records.Hold(record);

        return record.Subject;
    }

    private SubjectId Referenced(OrganizationId organization, string reference)
    {
        var record = new HeldRecord(SubjectId.New(Randomness), organization, Noon)
        {
            References = { reference },
        };

        _deployment.Records.Hold(record);

        return record.Subject;
    }
}
