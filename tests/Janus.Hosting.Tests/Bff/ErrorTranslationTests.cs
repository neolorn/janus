using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// What answers under the mount where neither a stage nor an endpoint wrote the
/// library's body: a path nothing serves, a method a path does not take, a fault, and a
/// refusal of the provider's that cannot go back to the client (LIB-API-003 AC4,
/// BFF-ERR-001, BFF-ERR-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class ErrorTranslationTests
{
    private const string Prefix = "/identity";

    /// <summary>
    /// LIB-API-003 AC4: a path under the mount that no endpoint serves is answered by
    /// the library's writer, as the absence of a record is, and not with the
    /// framework's bare status.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_003_AC4_APathNoEndpointServesAnswersTheEnvelopeAsync()
    {
        await using var deployment = new Deployment(prefix: Prefix);

        Answer answered = await new Browser(deployment).SendAsync("GET", Prefix + "/nowhere");

        AssertEnvelope(answered, StatusCodes.Status404NotFound, ErrorCodes.ResourceNotFound);
        Assert.Empty(answered.Json().GetProperty("details").EnumerateObject());
    }

    /// <summary>
    /// LIB-API-003 AC4: the host's own paths outside the mount answer as they did; the
    /// library answers nothing it was not mounted over.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_003_AC4_APathOutsideTheMountIsTheHostsAsync()
    {
        await using var deployment = new Deployment(prefix: Prefix);

        Answer answered = await new Browser(deployment).SendAsync("GET", "/nowhere");

        Assert.Equal(StatusCodes.Status404NotFound, answered.Status);
        Assert.Equal(string.Empty, answered.Body);
        Assert.Null(answered.Header(HeaderNames.ContentType));
    }

    /// <summary>
    /// LIB-API-003 AC4: a method a mapped path does not take is answered by the
    /// library's writer as a path it does not serve, which names none of the methods
    /// it does take.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_003_AC4_AMethodAPathDoesNotTakeAnswersTheEnvelopeAsync()
    {
        await using var deployment = new Deployment(prefix: Prefix);
        var browser = new Browser(deployment);

        _ = await browser.SendAsync("GET", Prefix + "/register");

        Answer answered = await browser.SendAsync("PUT", Prefix + "/register", "{}");

        AssertEnvelope(answered, StatusCodes.Status404NotFound, ErrorCodes.ResourceNotFound);
        Assert.Null(answered.Header(HeaderNames.Allow));
    }

    /// <summary>
    /// LIB-API-003 AC4, BFF-MACH-001: a route of the machine profile, which keeps the
    /// browser profile's stages off it, answers a method it does not take in the same
    /// way.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_003_AC4_AMethodAMachineRouteDoesNotTakeAnswersTheEnvelopeAsync()
    {
        await using var deployment = new Deployment(prefix: Prefix);

        Answer answered = await new Browser(deployment).SendAsync("GET", Prefix + "/auth/break-glass");

        AssertEnvelope(answered, StatusCodes.Status404NotFound, ErrorCodes.ResourceNotFound);
        Assert.Null(answered.Header(HeaderNames.Allow));
    }

    /// <summary>
    /// LIB-API-003 AC4, BFF-ERR-002 AC1 and AC2: an endpoint that throws is answered as
    /// a fault, with the correlation identifier and nothing of what was thrown, and
    /// what was thrown is found in the log by that identifier by its type alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_003_AC4_AnEndpointThatThrowsAnswersAFaultAsync()
    {
        await using var deployment = new Deployment(prefix: Prefix);
        var browser = new Browser(deployment);

        _ = await browser.SendAsync("GET", Prefix + "/register");

        deployment.Configuration.Unreachable = Settings.RegistrationSessionLifetime.Key;

        Answer answered = await browser.SendAsync("POST", Prefix + "/register", "{\"clientId\":\"web\"}");
        string correlation = answered.Text("correlationId");

        AssertEnvelope(answered, StatusCodes.Status500InternalServerError, ErrorCodes.SystemFault);
        Assert.Empty(answered.Json().GetProperty("details").EnumerateObject());
        Assert.DoesNotContain("db.internal", answered.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), answered.Body, StringComparison.Ordinal);
        Assert.Contains(
            deployment.Logs.Lines,
            line => line.Contains(correlation, StringComparison.Ordinal)
                && line.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
        Assert.DoesNotContain(deployment.Logs.Lines, line => line.Contains("db.internal", StringComparison.Ordinal));
    }

    /// <summary>
    /// LIB-API-003 AC4, AUTH-OIDC-006 AC2: an authorization request the provider
    /// refuses and cannot return to a client is answered to the browser by the
    /// library's writer, carrying the protocol's code and no description.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_003_AC4_AnAuthorizationRefusalAnswersTheEnvelopeAsync()
    {
        await using var deployment = new Deployment(prefix: Prefix);

        Answer answered = await new Browser(deployment).SendAsync("GET", Prefix + "/oidc/authorize");

        AssertEnvelope(answered, StatusCodes.Status400BadRequest, ErrorCodes.RequestMalformed);
        Assert.Equal(
            ["error"],
            answered.Json().GetProperty("details").EnumerateObject().Select(detail => detail.Name));
        Assert.Equal(
            "invalid_request",
            answered.Json().GetProperty("details").GetProperty("error").GetString());
        Assert.Null(answered.Location);
    }

    // The body every refusal of the library carries, and nothing a person reads.
    private static void AssertEnvelope(Answer answered, int status, ErrorCode code)
    {
        Assert.Equal(status, answered.Status);
        Assert.StartsWith("application/json", answered.Header(HeaderNames.ContentType), StringComparison.Ordinal);
        Assert.DoesNotContain("text/html", answered.Header(HeaderNames.ContentType), StringComparison.Ordinal);

        JsonElement body = answered.Json();

        Assert.Equal(
            ["code", "correlationId", "details"],
            body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(code.ToString(), body.GetProperty("code").GetString());
        Assert.NotEqual(string.Empty, body.GetProperty("correlationId").GetString());

        foreach (JsonProperty detail in body.GetProperty("details").EnumerateObject())
        {
            Assert.DoesNotContain(" ", detail.Value.ToString(), StringComparison.Ordinal);
        }
    }
}
