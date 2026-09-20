using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// What every endpoint of the library holds to, whatever it does: where it is
/// mounted, what a refusal carries, and what identifies the session
/// (API-CONV-001, API-CONV-002, API-CONV-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class ApiConventionTests
{
    private const string Prefix = "/identity/v1";

    /// <summary>
    /// API-CONV-001 AC1: the host decides the prefix by where it mounts the library,
    /// and every endpoint follows it without the library knowing a path.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_001_AC1_TheHostMountsTheLibraryWhereItLikesAsync()
    {
        await using var deployment = new Deployment(prefix: Prefix);
        var browser = new Browser(deployment);

        Answer mounted = await browser.SendAsync("GET", Prefix + "/register");
        Answer elsewhere = await browser.SendAsync("GET", "/register");

        Assert.Equal(StatusCodes.Status401Unauthorized, mounted.Status);
        Assert.Equal(StatusCodes.Status404NotFound, elsewhere.Status);

        // The two documents of REG-PM-001 sit at the site root by definition, so the
        // prefix does not move them.
        Assert.Equal(
            StatusCodes.Status404NotFound,
            (await browser.SendAsync("GET", "/.well-known/passkey-endpoints")).Status);
    }

    /// <summary>
    /// API-CONV-002 AC1: a refusal carries a code and structured data, and nothing
    /// in it is a sentence for a person to read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_002_AC1_NoRefusalCarriesASentenceAsync()
    {
        await using var deployment = new Deployment();
        var browser = new Browser(deployment);

        Answer refused = await browser.SendAsync("GET", "/register");
        JsonElement body = refused.Json();

        Assert.Equal(
            ["code", "correlationId", "details"],
            [.. body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)]);

        Assert.Equal(ErrorCodes.SessionExpired.ToString(), body.GetProperty("code").GetString());
        Assert.DoesNotContain(" ", body.GetProperty("code").GetString()!, StringComparison.Ordinal);
        Assert.Empty(body.GetProperty("details").EnumerateObject());
    }

    /// <summary>
    /// API-CONV-002 AC2: the identifier a refusal carries is the one the request was
    /// traced under, so the log and the trail answer to the same value.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_002_AC2_EveryRefusalCarriesItsCorrelationIdentifierAsync()
    {
        await using var deployment = new Deployment();
        var browser = new Browser(deployment);

        Answer first = await browser.SendAsync("GET", "/register");
        Answer second = await browser.SendAsync("GET", "/register");

        Assert.NotEqual(string.Empty, first.Text("correlationId"));
        Assert.NotEqual(first.Text("correlationId"), second.Text("correlationId"));
    }

    /// <summary>
    /// API-CONV-004 AC1: a state change without the token is refused with the status
    /// a failure that names no record takes, whatever else the request carries.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_004_AC1_AStateChangeWithoutTheTokenIsRefusedAsync()
    {
        await using var deployment = new Deployment();
        var browser = new Browser(deployment);

        _ = await browser.SendAsync("GET", "/register");

        Answer refused = await browser.SendAsync(
            "POST",
            "/register",
            "{\"clientId\":\"web\"}",
            header: true,
            token: false);

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.SessionCsrfInvalid.ToString(), refused.Text("code"));
    }

    /// <summary>
    /// API-CONV-004 AC2: the session is carried by the cookie and by nothing else,
    /// so no request body names one and the resolution reads no header or query.
    /// </summary>
    [Fact]
    public void API_CONV_004_AC2_NoEndpointTakesASessionIdentifier()
    {
        Assert.All(
            Fields(),
            named => Assert.DoesNotContain("session", named, StringComparison.OrdinalIgnoreCase));

        string resolution = Repository.Source("SessionResolution");

        Assert.Contains("Request.Cookies[BrowserCookies.Session]", resolution, StringComparison.Ordinal);
        Assert.DoesNotContain("Request.Headers", resolution, StringComparison.Ordinal);
        Assert.DoesNotContain("Request.Query", resolution, StringComparison.Ordinal);
    }

    // Every field of every request the endpoints read a body into.
    private static List<string> Fields()
    {
        var named = new List<string>();

        foreach (Type request in typeof(JanusEndpoints).Assembly.GetTypes())
        {
            if (!request.Name.EndsWith("Request", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (PropertyInfo property in request.GetProperties())
            {
                named.Add(property.Name);
            }
        }

        return named;
    }
}
