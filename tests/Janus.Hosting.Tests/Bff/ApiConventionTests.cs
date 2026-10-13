using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Net.Http.Headers;
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
    private const string Elsewhere = "unknown@example.test";
    private const string Another = "somebody@example.test";

    // The methods a browser asks an endpoint under.
    private static readonly string[] Methods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    // Subjects that stand for accounts no test of this class looks at again.
    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    // The identifier of a staged row, which is drawn fresh for every request.
    private static readonly Regex Identifiers = new(
        "\"id\":\"[0-9a-fA-F-]{36}\"",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

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
        // prefix does not move them: they answer there and not under it.
        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("GET", "/.well-known/passkey-endpoints")).Status);

        Assert.Equal(
            StatusCodes.Status404NotFound,
            (await browser.SendAsync("GET", Prefix + "/.well-known/passkey-endpoints")).Status);
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
    /// API-CONV-002 AC4 (D-179): the member the reader stopped at is named as the request
    /// writes it, with no <c>$</c> root and no list index, for each shape a body takes.
    /// </summary>
    [Fact]
    public void API_CONV_002_AC4_AnUnreadableMemberIsNamedAsTheRequestWritesIt()
    {
        Assert.Equal("name", Stopped<Dictionary<string, int>>("""{"name":"five"}"""));
        Assert.Equal(
            "inner.count",
            Stopped<Dictionary<string, Dictionary<string, int>>>("""{"inner":{"count":"many"}}"""));
        Assert.Equal(
            "items",
            Stopped<Dictionary<string, List<Dictionary<string, int>>>>("""{"items":[{"id":"one"}]}"""));
        Assert.Equal("items", Stopped<Dictionary<string, List<int>>>("""{"items":["one"]}"""));
        Assert.Equal("id", Stopped<List<Dictionary<string, int>>>("""[{"id":"one"}]"""));
    }

    /// <summary>
    /// BFF-ERR-001 AC1: no body the pipeline or an endpoint writes carries a sentence
    /// for a person to read, whichever of them refused the request.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC1_NoBodyCarriesASentenceAsync()
    {
        await using var deployment = new Deployment();
        var browser = new Browser(deployment);

        Answer stage = await browser.SendAsync(
            "POST",
            "/register",
            "{\"clientId\":\"web\"}",
            header: false);

        Answer endpoint = await browser.SendAsync("GET", "/account");

        foreach (Answer refused in new[] { stage, endpoint })
        {
            Assert.Equal(
                ["code", "correlationId", "details"],
                [.. refused.Json().EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)]);

            Assert.DoesNotContain(" ", refused.Text("code"), StringComparison.Ordinal);

            foreach (JsonProperty detail in refused.Json().GetProperty("details").EnumerateObject())
            {
                Assert.DoesNotContain(" ", detail.Value.ToString(), StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// BFF-ERR-001 AC2: the identifier a refusal carries is the one the request is
    /// traced under, which is what resolves it in the log and the trail.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC2_TheIdentifierIsTheOneTheRequestIsTracedUnderAsync()
    {
        await using var deployment = new Deployment();

        var context = new DefaultHttpContext { TraceIdentifier = "0HN000000000A:00000001" };

        context.Request.Method = "GET";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("identity.example.test");
        context.Request.Path = new PathString("/account");
        context.Request.Headers["Sec-Fetch-Site"] = "same-origin";
        context.Request.Headers.AcceptLanguage = "en";
        context.Request.Headers[BrowserCookies.RequestHeader] = "1";
        context.Request.Headers.Origin = "https://identity.example.test";

        var written = new ResponseBody();

        context.Response.Body = written;

        await deployment.SendAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains(
            "\"correlationId\":\"" + context.TraceIdentifier + "\"",
            written.Taken(),
            StringComparison.Ordinal);
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

        foreach (Type request in typeof(IdentityEndpoints).Assembly.GetTypes())
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

    /// <summary>
    /// API-CONV-002 AC2: a body the reader cannot parse is answered with the usual
    /// body, so the 400 carries a code and a correlation identifier like every other
    /// refusal, and its details name the member the reader stopped at.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task MapRegistration_ABodyThatDoesNotParse_AnswersTheUsualBodyAsync()
    {
        await using var deployment = new Deployment();

        Flow.Prepare(deployment);

        Browser browser = await Flow.BegunAsync(deployment);

        Answer refused = await browser.SendAsync("PUT", "/register/age", "{\"dateOfBirth\":");

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), refused.Text("code"));
        Assert.NotEmpty(refused.Text("correlationId"));
        Assert.Contains(
            "dateOfBirth",
            refused.Json().GetProperty("details").GetProperty("member").GetString()!,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// API-CONV-002 AC2: a body that parses but leaves out a member the endpoint
    /// requires is answered the same way, naming that member and nothing of its value.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task MapRegistration_ABodyMissingAMember_NamesTheMemberAsync()
    {
        await using var deployment = new Deployment();

        Flow.Prepare(deployment);

        Browser browser = await Flow.BegunAsync(deployment);

        Answer refused = await browser.SendAsync("PUT", "/register/email", ("value", string.Empty));

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), refused.Text("code"));
        Assert.NotEmpty(refused.Text("correlationId"));
        Assert.Equal(
            "value",
            refused.Json().GetProperty("details").GetProperty("member").GetString());
    }

    /// <summary>
    /// API-CONV-005 AC1, AUTH-ABUSE-003 AC1: the answer to an address another
    /// account holds is the answer to one nobody holds, byte for byte but for the
    /// correlation identifier every answer differs by.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_005_AC1_ADuplicateAddressAnswersAsAFreshOneDoesAsync()
    {
        await using var deployment = new Deployment();

        Flow.Prepare(deployment);

        // Both are begun at the same instant, so that the wait the shipped
        // restriction imposes between two messages to one address does not move the
        // expiry the state carries.
        Browser fresh = await Flow.BegunAsync(deployment);
        Browser held = await Flow.BegunAsync(deployment);

        _ = await fresh.SendAsync("PUT", "/register/age", ("dateOfBirth", "1990-01-01"));
        _ = await held.SendAsync("PUT", "/register/age", ("dateOfBirth", "1990-01-01"));

        Answer unknown = await fresh.SendAsync("PUT", "/register/email", ("value", Elsewhere));

        deployment.Directory.Held(IdentifierKind.Email, Elsewhere, SubjectId.New(Randomness));
        deployment.Clock.Advance(TimeSpan.FromMinutes(2));

        Answer known = await held.SendAsync("PUT", "/register/email", ("value", Elsewhere));

        Assert.Equal(unknown.Status, known.Status);
        Assert.Equal(Anonymous(unknown), Anonymous(known));
    }

    /// <summary>
    /// AUTH-ABUSE-003 AC1: adding an identifier to an account answers the same way
    /// whether or not another account holds it, which is the other endpoint
    /// API-CONV-005 names.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_003_AC1_AddingAHeldIdentifierAnswersAsAFreshOneDoesAsync()
    {
        Answer unknown = await AddedAsync(held: false);
        Answer known = await AddedAsync(held: true);

        Assert.Equal(StatusCodes.Status202Accepted, unknown.Status);
        Assert.Equal(unknown.Status, known.Status);
        Assert.Equal(Anonymous(unknown), Anonymous(known));
    }

    // One account adding the same address, in a deployment where somebody else holds
    // it and in one where nobody does.
    private static async Task<Answer> AddedAsync(bool held)
    {
        await using var deployment = new Deployment();

        Flow.Prepare(deployment);

        Browser browser = await Flow.SignedInAsync(deployment);

        if (held)
        {
            _ = deployment.Identifiers.Verified(SubjectId.New(Randomness), IdentifierKind.Email, Another);
        }

        return await browser.SendAsync(
            "POST",
            "/account/identifiers",
            ("kind", "email"),
            ("value", Another));
    }

    // The body with the one field that differs between any two answers taken out
    // (API-CONV-002 AC2), and the staged identifier's own value with it, which is
    // drawn per request and says nothing about existence.
    private static string Anonymous(Answer answer)
    {
        var written = new StringBuilder(answer.Body);

        if (answer.Body.Length is not 0 && answer.Json().ValueKind is JsonValueKind.Object)
        {
            foreach (JsonProperty property in answer.Json().EnumerateObject())
            {
                if (property.Name is "correlationId")
                {
                    _ = written.Replace(property.Value.GetString()!, string.Empty);
                }
            }
        }

        return Identifiers.Replace(written.ToString(), "\"id\":\"\"");
    }

    /// <summary>
    /// IDN-ACCT-003 AC1: nothing the library mounts takes two accounts, so no surface
    /// of it could combine them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_003_AC1_NoSurfaceTakesTwoAccountsAsync()
    {
        foreach (Type request in Requests())
        {
            IEnumerable<string> subjects = request
                .GetProperties()
                .Select(property => property.Name)
                .Where(Names);

            Assert.True(
                subjects.Count() <= 1,
                request.Name + " names more than one account");
        }

        await using var deployment = new Deployment();

        foreach (Endpoint endpoint in deployment.Endpoints)
        {
            if (endpoint is RouteEndpoint route && route.RoutePattern.RawText is { } pattern)
            {
                Assert.True(
                    route.RoutePattern.Parameters.Count(parameter => Names(parameter.Name)) <= 1,
                    pattern + " names more than one account");
            }
        }
    }

    /// <summary>
    /// API-LAND-001 AC4: no endpoint takes a link token in a path or a query, so a
    /// token reaches the library only in the body of a press and loading an address
    /// that carries one, as a mail scanner does, changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_LAND_001_AC4_NoEndpointTakesALinkTokenInAPathOrQueryAsync()
    {
        await using var deployment = new Deployment();

        foreach (RouteEndpoint route in deployment.Endpoints.OfType<RouteEndpoint>())
        {
            Assert.DoesNotContain(route.RoutePattern.Parameters, parameter => Carries(parameter.Name));

            if (route.Metadata.GetMetadata<MethodInfo>() is { } handler)
            {
                Assert.DoesNotContain(
                    handler.GetParameters(),
                    parameter => parameter.ParameterType == typeof(string) && Carries(parameter.Name ?? string.Empty));
            }
        }
    }

    /// <summary>
    /// API-CONV-003 AC4: every path the library serves, asked under a method it does
    /// not take, answers 404 <c>authz.resource.notfound</c> as a path nothing serves
    /// does; none answers 405 and none names the methods it takes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_003_AC4_AMethodAServedPathDoesNotTakeIsAnsweredAsNoPathAsync()
    {
        await using var deployment = new Deployment();
        var browser = new Browser(deployment);

        // The first answer sets the token every state-changing request then carries.
        _ = await browser.SendAsync("GET", "/register");

        RouteEndpoint[] routes = [.. deployment.Endpoints.OfType<RouteEndpoint>()];
        int asked = 0;

        foreach (string path in routes.Select(route => Concrete(route.RoutePattern)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var taken = new HashSet<string>(
                routes.Where(route => Serves(route.RoutePattern, path)).SelectMany(Taken),
                StringComparer.OrdinalIgnoreCase);

            foreach (string method in Methods.Where(method => !taken.Contains(method)))
            {
                // Each request comes from an address of its own, so the limit on a source
                // (BFF-ORDER-001) refuses none of them before its path is looked at.
                var source = new IPAddress([198, 18, (byte)(asked >> 8), (byte)asked]);
                Answer answered = await browser.SendAsync(
                    method,
                    path,
                    method is "GET" or "DELETE" ? null : "{}",
                    source: source);

                Assert.Equal(
                    (method, path, StatusCodes.Status404NotFound, ErrorCodes.ResourceNotFound.ToString()),
                    (method, path, answered.Status, answered.Text("code")));
                Assert.Null(answered.Header(HeaderNames.Allow));

                asked++;
            }
        }

        Assert.NotEqual(0, asked);
    }

    // A path the pattern serves, every parameter filled with one identifier.
    private static string Concrete(RoutePattern pattern) =>
        "/" + string.Join(
            '/',
            pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
            {
                RoutePatternLiteralPart literal => literal.Content,
                RoutePatternSeparatorPart separator => separator.Content,
                _ => "0198c5a2-7d3e-7b61-9c4f-5e2a1b3c4d5e",
            }))));

    // Whether the pattern serves the path: segment for segment, a literal as written
    // and a parameter whatever is there.
    private static bool Serves(RoutePattern pattern, string path)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return pattern.PathSegments.Count == segments.Length
            && pattern.PathSegments
                .Zip(segments)
                .All(pair => pair.First is not { IsSimple: true, Parts: [RoutePatternLiteralPart literal] }
                    || string.Equals(literal.Content, pair.Second, StringComparison.OrdinalIgnoreCase));
    }

    // The methods a route takes: the ones it names, or every one where it names none.
    private static IEnumerable<string> Taken(RouteEndpoint route) =>
        route.Metadata.GetMetadata<HttpMethodMetadata>() is { HttpMethods.Count: > 0 } named ? named.HttpMethods : Methods;

    // Whether a route or handler parameter is named for a link token.
    private static bool Carries(string named) =>
        named.Contains("token", StringComparison.OrdinalIgnoreCase);

    // Whether a field or a route parameter names an account rather than a record of
    // one kind or another.
    private static bool Names(string named) =>
        named.Contains("subject", StringComparison.OrdinalIgnoreCase)
        || named.Contains("account", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<Type> Requests() =>
        typeof(IdentityEndpoints).Assembly
            .GetTypes()
            .Where(request => request.Name.EndsWith("Request", StringComparison.Ordinal));

    // The member the shared reader names for a body that does not read as the type.
    private static string? Stopped<TBody>(string body)
    {
        JsonException unreadable = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<TBody>(body, JsonSerializerOptions.Web));

        return MalformedRequest.Member(unreadable.Path ?? string.Empty);
    }
}
