using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The application's JSON options are the host's: the library changes nothing of them,
/// and nothing a host sets in them changes a body the library reads or writes, every one
/// going through a library context under that context's own options (CONV-DESIGN-006,
/// CONV-DESIGN-007, D-206).
/// </summary>
[Trait("kind", "unit")]
public sealed partial class ApplicationJsonTests : IAsyncDisposable
{
    private const string Orders = "/host/orders";

    private const string Json = "application/json";

    private const string Ordered = """{"ORDER_NAME":"Weekly box","quantity":"4"}""";

    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly Deployment _deployment = new(host: MapOrders, json: Hostile);

    /// <summary>
    /// A deployment administered by one organization, whose host sets the application's
    /// JSON options as no library body is written and maps an endpoint of its own that
    /// reads and answers through them.
    /// </summary>
    public ApplicationJsonTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _deployment.DisposeAsync();

    /// <summary>
    /// CONV-DESIGN-007 AC9: after <c>AddJanus</c> the application's JSON options are as
    /// the host left them, so a host's own type, declared in no library context, is
    /// written and read as it is without the library, whether the host set the options
    /// or left them as the framework sets them.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC9_AHostTypeSerializesAsItDoesWithoutTheLibrary()
    {
        var order = new HostOrder("Weekly box", 3);

        foreach (Action<JsonOptions>? host in new Action<JsonOptions>?[] { null, Hostile })
        {
            JsonSerializerOptions without = Options(host, library: false);
            JsonSerializerOptions with = Options(host, library: true);

            Assert.Equal(
                without.TypeInfoResolverChain.Select(resolver => resolver.GetType()),
                with.TypeInfoResolverChain.Select(resolver => resolver.GetType()));
            Assert.Equal(JsonSerializer.Serialize(order, without), JsonSerializer.Serialize(order, with));
            Assert.Equal(JsonSerializer.Deserialize<HostOrder>(Ordered, without), JsonSerializer.Deserialize<HostOrder>(Ordered, with));
        }
    }

    /// <summary>
    /// CONV-DESIGN-007 AC9: a host endpoint's own body is read and answered through the
    /// application's JSON options as the host set them, beside the library's endpoints.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_007_AC9_AHostEndpointReadsAndAnswersAsTheHostSetsItAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer echoed = await browser.SendAsync("POST", Orders, body: Ordered);

        Assert.Equal(StatusCodes.Status200OK, echoed.Status);
        Assert.Equal("Weekly box", echoed.Text("order_name"));
        Assert.Equal(4, echoed.Json().GetProperty("quantity").GetInt32());
    }

    /// <summary>
    /// CONV-DESIGN-007 AC9: every request and response type of the library's endpoints,
    /// and the pipeline's answer, is declared in a library context; an image the library
    /// answers is no JSON body.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC9_EveryRequestAndResponseTypeIsDeclaredInALibraryContext()
    {
        RouteEndpoint[] endpoints = Library();
        Type[] bodies =
        [
            typeof(ApiError),
            .. endpoints
                .Select(endpoint => endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType)
                .OfType<Type>(),
            .. endpoints
                .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>())
                .Where(produced => produced.ContentTypes.Contains(Json, StringComparer.Ordinal))
                .Select(produced => produced.Type)
                .OfType<Type>()
                .Where(type => type != typeof(void)),
        ];

        Assert.Contains(endpoints, endpoint => endpoint.Metadata.GetMetadata<IAcceptsMetadata>() is not null);
        Assert.All(
            bodies.Distinct(),
            body => Assert.True(LibraryContexts.Declaring(body) is not null, body.Name + " is declared in no library context."));
    }

    /// <summary>
    /// CONV-DESIGN-007 AC9 (D-206): every endpoint of the library that reads a body reads
    /// it through a library context and declares the type it reads, and none leaves its
    /// body for the framework to read through the application's options: no handler
    /// takes the body as a parameter, each file reads every body type it declares and
    /// declares every one it reads, and the one reader of a request body is the library's.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC9_EveryBodyIsReadThroughALibraryContext()
    {
        RouteEndpoint[] reading =
        [
            .. Library().Where(endpoint => endpoint.Metadata.GetMetadata<IAcceptsMetadata>() is not null),
        ];
        string[] sources = Repository.Project("Janus.Hosting");

        Assert.NotEmpty(reading);
        Assert.All(
            reading,
            endpoint =>
            {
                Type body = endpoint.Metadata.GetMetadata<IAcceptsMetadata>()!.RequestType!;

                Assert.Single(endpoint.Metadata.GetOrderedMetadata<IAcceptsMetadata>());
                Assert.DoesNotContain(
                    endpoint.Metadata.GetMetadata<MethodInfo>()!.GetParameters(),
                    parameter => parameter.ParameterType == body);
            });
        Assert.All(
            sources,
            file =>
            {
                string source = File.ReadAllText(file);

                Assert.Equal(Named(Declared(), source), Named(Read(), source));
            });
        Assert.Equal(
            ["JsonBody.cs"],
            sources
                .Where(file => File.ReadAllText(file).Contains("ReadFromJsonAsync", StringComparison.Ordinal))
                .Select(Path.GetFileName));
    }

    /// <summary>
    /// CONV-DESIGN-007 AC9: with the application's options reading names in any case, a
    /// member of a library body spelled in another case is not read, so the endpoint
    /// refuses the body as one without it; spelled as <c>09</c> spells it, it is read and
    /// answered under the library's names, not the host's naming policy.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_007_AC9_AMemberSpelledInAnotherCaseIsNotReadAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer cased = await browser.SendAsync("POST", "/auth/begin", ("Identifier", Flow.Address));
        Answer spelled = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));

        Assert.Equal(StatusCodes.Status400BadRequest, cased.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), cased.Text("code"));
        Assert.Equal("identifier", cased.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.True(cased.Json().TryGetProperty("correlationId", out _));
        Assert.Equal(StatusCodes.Status200OK, spelled.Status);
        Assert.True(spelled.Json().TryGetProperty("challengeId", out _));
        Assert.False(spelled.Json().TryGetProperty("challenge_id", out _));
    }

    /// <summary>
    /// CONV-DESIGN-007 AC9: with the application's options reading numbers from strings,
    /// a member a library body reads as a number, given as a string, is refused 400
    /// <c>api.request.malformed</c> naming it; given as a number, the body is read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_007_AC9_ANumberGivenAsAStringIsRefusedNamingItAsync()
    {
        Browser administrator = await Flow.SignedInAsync(_deployment);

        foreach (Permission permission in new[] { Permissions.RestrictionEdit, Permissions.RestrictionGrant, Permissions.SystemAdminister })
        {
            _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, permission);
        }

        Answer quoted = await GrantedAsync(administrator, "3");
        Answer counted = await GrantedAsync(administrator, 3);

        Assert.Equal(StatusCodes.Status400BadRequest, quoted.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), quoted.Text("code"));
        Assert.Equal("credit", quoted.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal(StatusCodes.Status404NotFound, counted.Status);
        Assert.Equal(ErrorCodes.RestrictionNotFound.ToString(), counted.Text("code"));
    }

    // A host that names members in snake case, reads names in any case and reads numbers
    // from strings, none of which a body of 09 does.
    private static void Hostile(JsonOptions options)
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.SerializerOptions.PropertyNameCaseInsensitive = true;
        options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
    }

    private static void MapOrders(IEndpointRouteBuilder endpoints) =>
        _ = endpoints.MapPost(Orders, (HostOrder order) => TypedResults.Ok(order)).AllowNoSession();

    // The application's JSON options as a host sets them, with the library registered
    // after or not at all.
    private static JsonSerializerOptions Options(Action<JsonOptions>? host, bool library)
    {
        var services = new ServiceCollection();

        _ = services.AddOptions();

        if (host is not null)
        {
            _ = services.ConfigureHttpJsonOptions(host);
        }

        if (library)
        {
            _ = services.AddJanus("Host=unused", HostFixture.Declaration(), ApplicationKind.Public);
        }

        using ServiceProvider provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
    }

    private static Task<Answer> GrantedAsync(Browser browser, object credit) =>
        browser.SendAsync(
            "POST",
            "/admin/restrictions/no.such.restriction/grant",
            ("keyValue", "+201001234567"),
            ("credit", credit),
            ("reason", "their carrier dropped both codes"));

    // The body types a file names, as one set whether a type is written nullable or not.
    private static SortedSet<string> Named(Regex pattern, string source) =>
        new(
            pattern.Matches(source).Select(named => named.Groups[1].Value.Replace("?", string.Empty, StringComparison.Ordinal)),
            StringComparer.Ordinal);

    [GeneratedRegex(@"\.Accepts<(.+?)>\(MediaTypeNames", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Declared();

    [GeneratedRegex(@"JsonBody\s*\.ReadAsync<(.+?)>\(", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Read();

    private RouteEndpoint[] Library() =>
        [.. _deployment.Endpoints.OfType<RouteEndpoint>().Where(endpoint => EndpointDeclaration.Of(endpoint) is not null)];

    // A host's own body, which no library context declares.
    private sealed record HostOrder(string OrderName, int Quantity);
}
