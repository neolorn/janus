using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Outbox;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The self-service export of chapter 09 section 7: one routine in two formats, the
/// same data in both, and the rate limit that stands between a borrowed session and a
/// complete copy of a person's data (PRIV-RIGHT-003, D-086).
/// </summary>
[Trait("kind", "unit")]
public sealed class ExportEndpointTests : IAsyncDisposable
{
    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser.
    /// </summary>
    public ExportEndpointTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// PRIV-RIGHT-003 AC1: the readable and the portable arrangement carry the same
    /// data, so nothing is reachable through one that is not reachable through the
    /// other.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AC1_BothFormatsContainTheSameDataAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Held();

        Answer readable = await browser.SendAsync("GET", "/privacy/export?format=human");
        Answer portable = await browser.SendAsync("GET", "/privacy/export?format=machine");

        Assert.Equal(StatusCodes.Status200OK, readable.Status);
        Assert.Equal(StatusCodes.Status200OK, portable.Status);

        Assert.Equal(readable.Text("subject"), portable.Text("subject"));

        Assert.Equal(
            Flattened(readable.Json().GetProperty("sections")),
            Values(portable.Json().GetProperty("values")));
    }

    /// <summary>
    /// PRIV-RIGHT-003 AC2: the portable arrangement is flat and stable-named, every
    /// name being the section, the record's place in it and the field.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AC2_ThePortableFormatIsFlatAndStableNamedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Held();

        JsonElement values =
            (await browser.SendAsync("GET", "/privacy/export?format=machine"))
            .Json()
            .GetProperty("values");

        Assert.Equal("ahmed@example.test", values.GetProperty("identifiers.0.value").GetString());
        Assert.Equal("Email", values.GetProperty("identifiers.0.kind").GetString());
        Assert.Equal("true", values.GetProperty("identifiers.0.verified").GetString());
        Assert.Equal("+441632960011", values.GetProperty("identifiers.1.value").GetString());

        Assert.All(
            values.EnumerateObject(),
            value => Assert.Equal(JsonValueKind.String, value.Value.ValueKind));
    }

    /// <summary>
    /// PRIV-RIGHT-003: the readable arrangement is grouped and labelled, and is still
    /// structured data rather than a rendered document (D-058).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_TheReadableArrangementIsGroupedAndLabelledAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Held();

        JsonElement sections =
            (await browser.SendAsync("GET", "/privacy/export?format=human"))
            .Json()
            .GetProperty("sections");

        Assert.Equal(
            ["identifiers", "consents", "objections"],
            sections.EnumerateArray().Select(section => section.GetProperty("name").GetString()));

        Assert.Equal(
            2,
            sections.EnumerateArray().First().GetProperty("records").GetArrayLength());
    }

    /// <summary>
    /// D-054: two formats and no others, so a request naming a third one, or naming
    /// none, is malformed rather than served one of the two.
    /// </summary>
    /// <param name="query">What the request asked for.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("?format=")]
    [InlineData("?format=pdf")]
    [InlineData("?format=HUMAN")]
    public async Task PRIV_RIGHT_003_AFormatTheChapterDoesNotNameIsMalformedAsync(string query)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            (await browser.SendAsync("GET", "/privacy/export" + query)).Status);
    }

    /// <summary>
    /// D-086: the export is rate-limited, and the refusal says when the limit lifts.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_TheSpentRateLimitAnswersWithWhenItLiftsAsync()
    {
        _deployment.Configuration.Set(Settings.PrivacyExportRateLimit, 1);

        Browser browser = await Flow.SignedInAsync(_deployment);

        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("GET", "/privacy/export?format=human")).Status);

        Answer refused = await browser.SendAsync("GET", "/privacy/export?format=human");

        Assert.Equal(StatusCodes.Status429TooManyRequests, refused.Status);
        Assert.Equal(ErrorCodes.Throttled.ToString(), refused.Text("code"));
        Assert.NotEqual(
            default,
            refused.Json().GetProperty("details").GetProperty("retryAt").GetDateTimeOffset());
    }

    /// <summary>
    /// PRIV-RIGHT-005b: the host holds the half the library cannot produce, so an
    /// assembled export puts the fact on the outbox for the subscribers.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AnAssembledExportReachesTheSubscribersAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _ = await browser.SendAsync("GET", "/privacy/export?format=machine");

        Delivery raised = Assert.Single(
            _deployment.Outbox.Deliveries,
            delivery => delivery.Kind is SubjectEventKind.ExportRequested);

        Assert.Equal(Subject(), raised.Subject);
        Assert.Equal(Subject(), Assert.Single(_deployment.ExportLedger.Taken).Subject);
    }

    /// <summary>
    /// PRIV-RIGHT-003: the export is the subject's own, so a browser holding no
    /// session gets none of it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_ABrowserHoldingNoSessionGetsNoExportAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            (await browser.SendAsync("GET", "/privacy/export?format=human")).Status);
    }

    private static Dictionary<string, string> Flattened(JsonElement sections)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (JsonElement section in sections.EnumerateArray())
        {
            string name = section.GetProperty("name").GetString() ?? string.Empty;
            int index = 0;

            foreach (JsonElement record in section.GetProperty("records").EnumerateArray())
            {
                foreach (JsonProperty field in record.EnumerateObject())
                {
                    values[name + "." + index + "." + field.Name] =
                        field.Value.GetString() ?? string.Empty;
                }

                index++;
            }
        }

        return values;
    }

    private static Dictionary<string, string> Values(JsonElement flat)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (JsonProperty value in flat.EnumerateObject())
        {
            values[value.Name] = value.Value.GetString() ?? string.Empty;
        }

        return values;
    }

    private SubjectId Subject() => _deployment.Directory.Created[^1].Subject;

    private void Held() =>
        _deployment.ExportSource.Holds(
            Subject(),
            new ExportSection(
                "identifiers",
                [
                    new ExportRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["kind"] = "Email",
                        ["value"] = "ahmed@example.test",
                        ["verified"] = "true",
                    }),
                    new ExportRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["kind"] = "Phone",
                        ["value"] = "+441632960011",
                        ["verified"] = "false",
                    }),
                ]));
}
