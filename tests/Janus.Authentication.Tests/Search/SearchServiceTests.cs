using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Authentication.Search;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Organizations;
using Janus.Authentication.Tests.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Search;

/// <summary>
/// The staff search as a host calls it: what a query finds, who is answered what, and
/// what the search counts and records (IDN-SRCH-001 to IDN-SRCH-003, D-256).
/// </summary>
[Trait("kind", "unit")]
public sealed class SearchServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 30, TimeSpan.Zero);

    private readonly MembershipLookupInMemory _memberships = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly ReadVolumeInMemory _volume = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly SearchSubjectsInMemory _subjects = new();
    private readonly SearchWindowsInMemory _windows = new();
    private readonly SearchLogInMemory _log = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly OrganizationsInMemory _organizations;
    private readonly OrganizationId _staff = new(Guid.CreateVersion7());
    private readonly OrganizationId _clinic = new(Guid.CreateVersion7());
    private readonly OrganizationId _pharmacy = new(Guid.CreateVersion7());
    private readonly SubjectId _caller = new(Guid.NewGuid());
    private readonly SearchSubject _fatma;
    private readonly SearchSubject _ahmed;
    private readonly SearchSubject _customer;
    private readonly SearchSubject _guest;
    private readonly SearchSubject _visitor;
    private SearchIndexHolder _holder = new(served: true);

    /// <summary>
    /// A deployment with its administrative organization and two business
    /// organizations: a clinic with a member and a record, a pharmacy with a record, and
    /// a customer who is a member of none.
    /// </summary>
    public SearchServiceTests()
    {
        _organizations = new OrganizationsInMemory(_memberships);
        _organizations.Seed(_staff, administrative: true);
        _organizations.Seed(_clinic);
        _organizations.Seed(_pharmacy);
        _administrative.Organization = _staff;
        _gate.Administrative = _staff;

        _fatma = Hold(SubjectKind.Account, "فاطمة الزهراء", ["fatma@example.com"], ["+201001239876"], _clinic);
        _ahmed = Hold(SubjectKind.Account, "Ahmed Hassan", ["ahmed.hassan@example.com"], ["+201112223344"], _clinic);
        _customer = Hold(SubjectKind.Account, "Ahmed Adel", ["adel@example.com"], [], null);
        _guest = Hold(SubjectKind.Record, "Ahmed Samir", ["samir@example.org"], [], _clinic);
        _visitor = Hold(SubjectKind.Record, "Ahmed Nabil", [], [], _pharmacy);
        _holder.Swap(_subjects.Indexed());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _holder.Dispose();
        await _work.DisposeAsync();
    }

    private SearchService Search =>
        new(
            _holder,
            new ReadAdmissionInMemory(_gate, _administrative, _memberships),
            _organizations,
            _administrative,
            _subjects,
            _windows,
            _volume,
            _configuration,
            _work,
            _log,
            _clock);

    private AccessContext Asking => AccessContext.Of(_caller);

    /// <summary>
    /// IDN-SRCH-001 AC1: a query writing ة as ه, or carrying harakat and a tatweel, finds
    /// the name written the other way.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("فاطمه")]
    [InlineData("فَاطِمـــة")]
    public async Task IDN_SRCH_001_AC1_AnArabicSpellingVariantFindsTheNameAsync(string query)
    {
        Both(_clinic);

        IReadOnlyList<SearchResult> found = Succeeded(await SearchAsync(_clinic, query));

        Assert.Equal([_fatma.Subject], found.Select(result => result.Subject));
    }

    /// <summary>
    /// IDN-SRCH-001 AC4: the last four digits of a verified phone, written in
    /// Arabic-Indic digits, find the account holding it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_001_AC4_APhonesLastDigitsInArabicIndicDigitsFindItAsync()
    {
        Both(_clinic);

        IReadOnlyList<SearchResult> found = Succeeded(await SearchAsync(_clinic, "٩٨٧٦"));

        Assert.Equal([new SearchResult(_fatma.Subject, SubjectKind.Account, _fatma.DisplayName, SearchMatch.Phone)], found);
    }

    /// <summary>
    /// IDN-SRCH-001 AC8: a result carries its display name and no email or phone value,
    /// and one whose name and email both match is answered with <c>matched</c>
    /// <c>name</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_001_AC8_AResultCarriesNoContactAndANameMatchComesFirstAsync()
    {
        Both(_clinic);

        IReadOnlyList<SearchResult> found = Succeeded(await SearchAsync(_clinic, "hassan"));

        Assert.Equal([new SearchResult(_ahmed.Subject, SubjectKind.Account, "Ahmed Hassan", SearchMatch.Name)], found);
    }

    /// <summary>
    /// IDN-SRCH-001 AC9: a kind outside the vocabulary and a limit of 0 or 51 are refused
    /// 400 <c>api.request.malformed</c> naming the member; a query normalising to nothing
    /// or to more than 64 characters is refused 422 <c>api.request.invalid</c> naming
    /// <c>q</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_001_AC9_AQueryOutsideItsBoundsIsRefusedNamingTheMemberAsync()
    {
        Both(_clinic);

        Error kinds = Refused(await SearchAsync(_clinic, "ahmed", (SearchKinds)4));
        Error none = Refused(await SearchAsync(_clinic, "ahmed", limit: 0));
        Error many = Refused(await SearchAsync(_clinic, "ahmed", limit: 51));
        Error empty = Refused(await SearchAsync(_clinic, " .,- "));
        Error tooLong = Refused(await SearchAsync(_clinic, new string('a', 65)));
        IReadOnlyList<SearchResult> longest = Succeeded(await SearchAsync(_clinic, new string('a', 64)));

        Assert.Equal((ErrorCodes.RequestMalformed, "kinds"), Named(kinds));
        Assert.Equal((ErrorCodes.RequestMalformed, "limit"), Named(none));
        Assert.Equal((ErrorCodes.RequestMalformed, "limit"), Named(many));
        Assert.Equal((ErrorCodes.RequestInvalid, "q"), Named(empty));
        Assert.Equal((ErrorCodes.RequestInvalid, "q"), Named(tooLong));
        Assert.Empty(longest);
    }

    /// <summary>
    /// IDN-SRCH-001 AC11: what <see cref="SearchForm.Of"/> answers for a name is the text
    /// the index holds for it, and for a query the text the search matches.
    /// </summary>
    [Fact]
    public void IDN_SRCH_001_AC11_TheNormaliserIsTheIndexsAndTheQuerys()
    {
        var held = IndexedSubject.Of(_fatma);

        Assert.Equal(SearchForm.Of("فاطمة الزهراء"), held.Name);
        Assert.Equal(["201001239876"], held.Phones);
        Assert.Equal("فاطمه", SearchForm.Of("فاطمة"));
    }

    /// <summary>
    /// IDN-SRCH-002 AC1: a holder of <c>contact:read</c> alone, asking in an organization,
    /// is answered its current members and no record; a holder of <c>record:read</c>
    /// alone is answered the records it holds and no account.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_002_AC1_EachPermissionAnswersItsOwnKindAloneAsync()
    {
        _gate.Grant(_caller, _clinic, Permissions.ContactRead);

        IReadOnlyList<SearchResult> accounts = Succeeded(await SearchAsync(_clinic, "ahmed"));

        _gate.Revoke(_caller, _clinic, Permissions.ContactRead);
        _gate.Grant(_caller, _clinic, Permissions.RecordRead);

        IReadOnlyList<SearchResult> records = Succeeded(await SearchAsync(_clinic, "ahmed"));

        Assert.Equal([_ahmed.Subject], accounts.Select(result => result.Subject));
        Assert.Equal([_guest.Subject], records.Select(result => result.Subject));
        Assert.Empty(_gate.Refusals);
    }

    /// <summary>
    /// IDN-SRCH-002 AC3: a holder of both permissions in the administrative organization
    /// is answered an account with no membership and a record of any organization; an
    /// erased account is not answered, even before the index has heard of its erasure.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_002_AC3_TheAdministrativeOrganizationAnswersEverySubjectButTheErasedAsync()
    {
        Both(_staff);
        _subjects.Erase(_ahmed.Subject);

        IReadOnlyList<SearchResult> found = Succeeded(await SearchAsync(_staff, "ahmed"));

        Assert.Equal([_customer.Subject, _visitor.Subject, _guest.Subject], found.Select(result => result.Subject));
    }

    /// <summary>
    /// IDN-SRCH-002 AC4: a caller holding neither permission, and a holder of
    /// <c>contact:read</c> alone asking for records, are refused 403 <c>authz.denied</c>
    /// with the refusal recorded once; a holder of both asking for no kind is answered
    /// accounts and records.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_002_AC4_ACallerWhoMayReadNoKindAskedIsRefusedAndRecordedAsync()
    {
        Error neither = Refused(await SearchAsync(_clinic, "ahmed"));
        int recordedForNeither = _gate.Refusals.Count;

        _gate.Grant(_caller, _clinic, Permissions.ContactRead);

        Error recordsAlone = Refused(await SearchAsync(_clinic, "ahmed", SearchKinds.Records));
        int recordedForRecords = _gate.Refusals.Count - recordedForNeither;

        _gate.Grant(_caller, _clinic, Permissions.RecordRead);

        IReadOnlyList<SearchResult> both = Succeeded(await SearchAsync(_clinic, "ahmed"));

        Assert.Equal(ErrorCodes.Denied, neither.Code);
        Assert.Equal(ErrorCodes.Denied, recordsAlone.Code);
        Assert.Equal(1, recordedForNeither);
        Assert.Equal(1, recordedForRecords);
        Assert.Equal([_ahmed.Subject, _guest.Subject], both.Select(result => result.Subject));
    }

    /// <summary>
    /// IDN-SRCH-002 AC5: an organization the deployment does not hold answers 404
    /// <c>identity.organization.notfound</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_002_AC5_AnOrganizationNotHeldIsNotFoundAsync()
    {
        Both(_staff);

        Error refused = Refused(await SearchAsync(new OrganizationId(Guid.CreateVersion7()), "ahmed"));

        Assert.Equal(ErrorCodes.OrganizationNotFound, refused.Code);
    }

    /// <summary>
    /// IDN-SRCH-002 AC6: a search in a system principal's context, a host's job's
    /// included, is refused 403 <c>authz.denied</c> and answers no result.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_002_AC6_ASystemPrincipalMayNotSearchAsync()
    {
        var job = AccessContext.Of(SystemPrincipal.ForOrganization("nightly-digest", "the nightly digest", _clinic));

        Result<IReadOnlyList<SearchResult>> searched = await Search.SearchAsync(
            job,
            new SearchQuery(_clinic, "ahmed", SearchKinds.None, 20),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Denied, Refused(searched).Code);
        Assert.Empty(_windows.Rows);
    }

    /// <summary>
    /// IDN-SRCH-002 AC7: past <c>search.perminute</c> searches inside a minute, a search
    /// is answered 429 <c>auth.throttled</c> with <c>retryAt</c> the next minute.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_002_AC7_ASearchPastTheMinutesLimitIsThrottledAsync()
    {
        Both(_clinic);
        _configuration.Set(Settings.SearchPerMinute, 2);

        _ = Succeeded(await SearchAsync(_clinic, "ahmed"));
        _ = Succeeded(await SearchAsync(_clinic, "ahmed"));
        Error throttled = Refused(await SearchAsync(_clinic, "ahmed"));

        Assert.Equal(ErrorCodes.Throttled, throttled.Code);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 12, 1, 0, TimeSpan.Zero), throttled.Details["retryAt"].GetDateTimeOffset());
    }

    /// <summary>
    /// IDN-SRCH-002 AC8: a holder of <c>contact:read</c> in the administrative
    /// organization, searching in another organization, is answered that organization's
    /// current members.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_002_AC8_AnAdministrativeHolderSearchesAnotherOrganizationAsync()
    {
        _gate.Grant(_caller, _staff, Permissions.ContactRead);

        IReadOnlyList<SearchResult> found = Succeeded(await SearchAsync(_clinic, "ahmed"));

        Assert.Equal([_ahmed.Subject], found.Select(result => result.Subject));
    }

    /// <summary>
    /// IDN-SRCH-002 AC9: a search answering its results adds as many to the caller's read
    /// volume.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_002_AC9_EachResultIsCountedTowardTheReadVolumeAsync()
    {
        Both(_staff);

        IReadOnlyList<SearchResult> found = Succeeded(await SearchAsync(_staff, "ahmed"));

        Assert.Equal(4, found.Count);
        Assert.Equal([(_caller, 4)], _volume.Returned.Select(counted => (counted.Context.Acting, counted.Records)));
    }

    /// <summary>
    /// OPS-ALERT-005 AC4: a refused search adds nothing to the caller's read volume.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_AC4_ARefusedSearchAddsNothingToTheReadVolumeAsync()
    {
        Result<IReadOnlyList<SearchResult>> refused = await SearchAsync(_clinic, "ahmed");

        Assert.Equal(ErrorCodes.Denied, refused.Match<ErrorCode?>(_ => null, error => error.Code));
        Assert.Empty(_volume.Returned);
    }

    /// <summary>
    /// IDN-SRCH-002 AC10: a search writes one log entry, holding its result count and
    /// its duration and nothing of the query.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_002_AC10_ASearchLogsItsCountAndDurationAloneAsync()
    {
        Both(_clinic);

        _ = Succeeded(await SearchAsync(_clinic, "ahmed"));

        Assert.Equal(2, Assert.Single(_log.Searched).Results);
    }

    /// <summary>
    /// IDN-SRCH-003 AC1: a process whose registration does not declare the search
    /// answers 503 <c>identity.search.unavailable</c> with <c>details.reason</c>
    /// <c>off</c>, and counts no search.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC1_AProcessThatServesNoSearchAnswersOffAsync()
    {
        Both(_clinic);
        _holder = new SearchIndexHolder(served: false);

        Error refused = Refused(await SearchAsync(_clinic, "ahmed"));

        Assert.Equal((ErrorCodes.SearchUnavailable, "off"), Reason(refused));
        Assert.Empty(_windows.Rows);
    }

    /// <summary>
    /// IDN-SRCH-003 AC2: until its index is built, a process that serves the search
    /// answers <c>building</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC2_UntilTheIndexIsBuiltTheSearchAnswersBuildingAsync()
    {
        Both(_clinic);
        _holder = new SearchIndexHolder(served: true);

        Error refused = Refused(await SearchAsync(_clinic, "ahmed"));

        Assert.Equal((ErrorCodes.SearchUnavailable, "building"), Reason(refused));
    }

    /// <summary>
    /// A process that withheld its index for a failed memory check or for its capacity
    /// answers why.
    /// </summary>
    /// <param name="withheld">Why the index was withheld.</param>
    /// <param name="reason">What the search answers.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData((int)SearchUnavailability.Unsafe, "unsafe")]
    [InlineData((int)SearchUnavailability.Capacity, "capacity")]
    public async Task SearchAsync_AnIndexWithheld_AnswersWhyAsync(int withheld, string reason)
    {
        Both(_clinic);
        _holder.Withhold((SearchUnavailability)withheld);

        Error refused = Refused(await SearchAsync(_clinic, "ahmed"));

        Assert.Equal((ErrorCodes.SearchUnavailable, reason), Reason(refused));
    }

    private static IReadOnlyList<SearchResult> Succeeded(Result<IReadOnlyList<SearchResult>> searched) =>
        searched.Match(results => results, error => throw new InvalidOperationException(error.Code.ToString()));

    private static Error Refused(Result<IReadOnlyList<SearchResult>> searched) =>
        searched.Match(_ => throw new InvalidOperationException("The search was admitted."), error => error);

    private static (ErrorCode Code, string? Member) Named(Error error) =>
        (error.Code, error.Details.TryGetValue("member", out JsonElement member) ? member.GetString() : null);

    private static (ErrorCode Code, string? Reason) Reason(Error error) =>
        (error.Code, error.Details.TryGetValue("reason", out JsonElement reason) ? reason.GetString() : null);

    private SearchSubject Hold(
        SubjectKind kind,
        string name,
        IReadOnlyList<string> emails,
        IReadOnlyList<string> phones,
        OrganizationId? organization)
    {
        var subject = new SearchSubject(
            new SubjectId(Guid.NewGuid()),
            kind,
            name,
            emails,
            phones,
            organization is OrganizationId held ? [held] : []);

        _subjects.Hold(subject);

        return subject;
    }

    private void Both(OrganizationId organization)
    {
        _gate.Grant(_caller, organization, Permissions.ContactRead);
        _gate.Grant(_caller, organization, Permissions.RecordRead);
    }

    private ValueTask<Result<IReadOnlyList<SearchResult>>> SearchAsync(
        OrganizationId organization,
        string query,
        SearchKinds kinds = SearchKinds.None,
        int limit = 20) =>
        Search.SearchAsync(Asking, new SearchQuery(organization, query, kinds, limit), TestContext.Current.CancellationToken);
}
