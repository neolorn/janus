using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Search;
using Janus.Core;
using Janus.Hosting.Search;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Search;

/// <summary>
/// The staff search's route: <c>POST /admin/search</c>, its default limit, the requests it
/// refuses, and what it answers while the process serves no index (IDN-SRCH-001,
/// IDN-SRCH-003, LIB-HOST-001, D-256).
/// </summary>
[Trait("kind", "unit")]
public sealed class SearchEndpointTests : IAsyncDisposable
{
    private const string Route = "/admin/search";

    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public SearchEndpointTests() => Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _randomness.Dispose();
        await _deployment.DisposeAsync();
    }

    /// <summary>
    /// IDN-SRCH-001 AC7: a search naming no limit is answered at most 20 results, sorted
    /// by normalised display name, and each result carries its subject, its kind, its name
    /// and the field that matched, and no email or phone value.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_001_AC7_ASearchNamingNoLimitIsAnsweredTwentyAsync()
    {
        Browser browser = await ReadingAsync();

        foreach (int at in Enumerable.Range(0, 25))
        {
            _deployment.SearchSubjects.Hold(new SearchSubject(
                SubjectId.New(_randomness),
                SubjectKind.Account,
                "Ahmed " + (char)('a' + at),
                ["ahmed" + at + "@example.test"],
                [],
                []));
        }

        _deployment.SearchHolder.Swap(_deployment.SearchSubjects.Indexed());

        Answer answered = await browser.SendAsync("POST", Route, Body("ahmed"));
        JsonElement[] results = [.. answered.Json().GetProperty("results").EnumerateArray()];

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Equal(20, results.Length);
        Assert.Equal(
            [.. Enumerable.Range(0, 20).Select(at => "Ahmed " + (char)('a' + at))],
            results.Select(result => result.GetProperty("displayName").GetString()));
        Assert.All(results, result =>
        {
            Assert.Equal("account", result.GetProperty("kind").GetString());
            Assert.Equal("name", result.GetProperty("matched").GetString());
            Assert.Equal(
                ["displayName", "kind", "matched", "subject"],
                result.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
        });
    }

    /// <summary>
    /// CONV-LOG-003 AC4, IDN-SRCH-002 AC10: with every level on, a search writes no log
    /// entry holding its query or a value the index holds, and its one Debug entry holds
    /// the result count and the duration alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_LOG_003_AC4_NoLogEntryOfASearchHoldsItsQueryOrAnIndexedValueAsync()
    {
        Browser browser = await ReadingAsync();

        _deployment.SearchSubjects.Hold(new SearchSubject(
            SubjectId.New(_randomness),
            SubjectKind.Account,
            "Zubaydah Qasim",
            ["zubaydah.qasim@example.test"],
            ["+201005550123"],
            []));
        _deployment.SearchHolder.Swap(_deployment.SearchSubjects.Indexed());

        int before = _deployment.Entries.Written.Count;
        Answer answered = await browser.SendAsync("POST", Route, Body("zubaydah"));
        string[] written = [.. _deployment.Entries.Written.Skip(before).Select(entry => entry.Line)];
        string[] searched =
        [
            .. _deployment.Entries.Written
                .Skip(before)
                .Where(entry => entry.Category == typeof(SearchLog).FullName)
                .Select(entry => entry.Level + " " + entry.Line),
        ];

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.All(
            ["zubaydah", "qasim", "example.test", "5550123"],
            value => Assert.DoesNotContain(
                written.Concat(_deployment.Logs.Lines),
                line => line.Contains(value, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(
            ["Milliseconds", "Results=1"],
            Assert.Single(searched)
                .Split(' ')
                .Where(part => part.Contains('=', StringComparison.Ordinal))
                .Select(part => part.StartsWith("Milliseconds=", StringComparison.Ordinal) ? "Milliseconds" : part)
                .Where(part => part != "{OriginalFormat}=A")
                .Order(StringComparer.Ordinal));
        Assert.StartsWith("Debug ", searched[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// IDN-SRCH-001 AC9: a <c>kinds</c> member other than <c>account</c> and
    /// <c>record</c>, a <c>limit</c> of 0 or 51 and a member the body does not take are
    /// each refused 400 <c>api.request.malformed</c> naming the member, and a <c>q</c>
    /// normalising to nothing or to more than 64 characters is refused 422
    /// <c>api.request.invalid</c> naming <c>q</c>.
    /// </summary>
    /// <param name="body">The body sent.</param>
    /// <param name="status">The status it is refused with.</param>
    /// <param name="code">The code it is refused with.</param>
    /// <param name="member">The member the refusal names.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("""{"organization":"33333333-3333-4333-8333-333333333333","q":"ahmed","kinds":["person"]}""", 400, "api.request.malformed", "kinds")]
    [InlineData("""{"organization":"33333333-3333-4333-8333-333333333333","q":"ahmed","limit":0}""", 400, "api.request.malformed", "limit")]
    [InlineData("""{"organization":"33333333-3333-4333-8333-333333333333","q":"ahmed","limit":51}""", 400, "api.request.malformed", "limit")]
    [InlineData("""{"organization":"33333333-3333-4333-8333-333333333333","q":"ahmed","page":2}""", 400, "api.request.malformed", "page")]
    [InlineData("""{"organization":"33333333-3333-4333-8333-333333333333","q":" .,- "}""", 422, "api.request.invalid", "q")]
    [InlineData("""{"organization":"33333333-3333-4333-8333-333333333333","q":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""", 422, "api.request.invalid", "q")]
    public async Task IDN_SRCH_001_AC9_AMalformedOrInvalidSearchIsRefusedNamingTheMemberAsync(
        string body,
        int status,
        string code,
        string member)
    {
        Browser browser = await ReadingAsync();

        _deployment.SearchHolder.Swap(_deployment.SearchSubjects.Indexed());

        Answer refused = await browser.SendAsync("POST", Route, body);

        Assert.Equal(status, refused.Status);
        Assert.Equal(code, refused.Text("code"));
        Assert.Equal(member, refused.Json().GetProperty("details").GetProperty("member").GetString());
    }

    /// <summary>
    /// IDN-SRCH-003 AC2: until the index is built, the search answers 503
    /// <c>identity.search.unavailable</c> with <c>details.reason</c> <c>building</c> and
    /// <c>Retry-After</c> 30 seconds.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC2_ASearchBeforeTheIndexIsBuiltIsAnsweredBuildingAsync()
    {
        Browser browser = await ReadingAsync();

        Answer building = await browser.SendAsync("POST", Route, Body("ahmed"));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, building.Status);
        Assert.Equal(ErrorCodes.SearchUnavailable.ToString(), building.Text("code"));
        Assert.Equal("building", building.Json().GetProperty("details").GetProperty("reason").GetString());
        Assert.Equal("30", building.Header("Retry-After"));
    }

    /// <summary>
    /// IDN-SRCH-003 AC1, LIB-HOST-001 AC8: in a process whose registration does not
    /// declare the search, the route and the call answer 503
    /// <c>identity.search.unavailable</c> with <c>details.reason</c> <c>off</c>, and the
    /// route carries no <c>Retry-After</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC1_AProcessNotServingTheSearchAnswersOffAsync()
    {
        await _deployment.DisposeAsync();
        _deployment = new Deployment(search: StaffSearch.Off);
        Prepare(_deployment);

        Browser browser = await ReadingAsync();
        Answer off = await browser.SendAsync("POST", Route, Body("ahmed"));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, off.Status);
        Assert.Equal(ErrorCodes.SearchUnavailable.ToString(), off.Text("code"));
        Assert.Equal("off", off.Json().GetProperty("details").GetProperty("reason").GetString());
        Assert.Null(off.Header("Retry-After"));
    }

    private static void Prepare(Deployment deployment)
    {
        Flow.Prepare(deployment);
        deployment.Administers(Administration);
    }

    private static string Body(string query) =>
        JsonSerializer.Serialize(new { organization = Administration.Value, q = query });

    // A signed-in caller holding contact:read in the administrative organization.
    private async Task<Browser> ReadingAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.ContactRead);

        return browser;
    }
}
