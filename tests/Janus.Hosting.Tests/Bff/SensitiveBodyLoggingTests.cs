using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// What request logging records of a body, with the library registered as a host
/// registers it and the host's own logging turned up to everything (BFF-LOG-002,
/// CONV-LOG-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class SensitiveBodyLoggingTests : IDisposable
{
    private const HttpLoggingFields Bodies = HttpLoggingFields.RequestBody | HttpLoggingFields.ResponseBody;

    // A value no log line has any other reason to hold.
    private const string Field = "a-declared-sensitive-value-5d0c";

    private const string Body = "{\"text\":\"" + Field + "\"}";

    private readonly LogsInMemory _logs = new();

    /// <summary>
    /// BFF-LOG-002 AC1: the library turns no body logging on, so a deployment that
    /// says nothing about it logs no body.
    /// </summary>
    [Fact]
    public void BFF_LOG_002_AC1_BodyLoggingIsOffByDefault()
    {
        using WebApplication application = Registered(everything: false);

        HttpLoggingFields fields = application.Services
            .GetRequiredService<IOptions<HttpLoggingOptions>>()
            .Value
            .LoggingFields;

        Assert.Equal(HttpLoggingFields.None, fields & Bodies);
    }

    /// <summary>
    /// BFF-LOG-002 AC1, AC2, CONV-LOG-003 AC2: with every field the logging has turned
    /// on, and asked for again on the endpoint itself, no log entry holds a field of a
    /// marked endpoint's body; the same body on an unmarked endpoint is logged, so the
    /// logging was there to leak it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_LOG_002_AC2_NoLogEntryHoldsAFieldOfAMarkedEndpointsBodyAsync()
    {
        using WebApplication application = Registered(everything: true);

        _ = application.MapPost("/marked", (JsonElement body) => Results.Ok(body))
            .WithMetadata(new SensitiveBodyAttribute())
            .WithHttpLogging(HttpLoggingFields.All);
        _ = application.MapPost("/plain", (JsonElement body) => Results.Ok(body));

        RequestDelegate pipeline = Pipeline(application);

        Assert.Equal(StatusCodes.Status200OK, await PostAsync(application, pipeline, "/marked"));
        Assert.NotEmpty(Logged());
        Assert.DoesNotContain(Logged(), line => line.Contains(Field, StringComparison.Ordinal));

        Assert.Equal(StatusCodes.Status200OK, await PostAsync(application, pipeline, "/plain"));
        Assert.Contains(Logged(), line => line.Contains(Field, StringComparison.Ordinal));
    }

    /// <summary>
    /// BFF-LOG-002 AC1, CONV-LOG-003: every endpoint the library maps carries the
    /// mark, since each body carries a credential or a person's data.
    /// </summary>
    [Fact]
    public void BFF_LOG_002_AC1_EveryEndpointTheLibraryMapsIsMarked()
    {
        using WebApplication application = Registered(everything: true);

        _ = application.MapIdentityEndpoints();

        RouteEndpoint[] mapped =
        [
            .. ((IEndpointRouteBuilder)application).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>(),
        ];

        Assert.NotEmpty(mapped);
        Assert.All(
            mapped,
            endpoint => Assert.NotNull(endpoint.Metadata.GetMetadata<SensitiveBodyAttribute>()));
    }

    /// <summary>
    /// BFF-LOG-002 AC1: a request the logging meets before its endpoint is known is
    /// treated as marked, so a host that logs ahead of routing logs no body at all.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_LOG_002_AC1_ABodyMetBeforeRoutingIsNotLoggedAsync()
    {
        using WebApplication application = Registered(everything: true);

        _ = application.MapPost("/plain", (JsonElement body) => Results.Ok(body));

        _ = ((IApplicationBuilder)application).UseHttpLogging();
        _ = ((IApplicationBuilder)application).UseRouting();
        _ = ((IApplicationBuilder)application).UseEndpoints(_ => { });

        RequestDelegate pipeline = ((IApplicationBuilder)application).Build();

        Assert.Equal(StatusCodes.Status200OK, await PostAsync(application, pipeline, "/plain"));
        Assert.NotEmpty(Logged());
        Assert.DoesNotContain(Logged(), line => line.Contains(Field, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public void Dispose() => _logs.Dispose();

    // The library as a host registers it, beside the host's own request logging:
    // everything the logging can record where the test asks, and the framework's
    // defaults where it does not.
    private WebApplication Registered(bool everything)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace).AddProvider(_logs);

        if (everything)
        {
            _ = builder.Services.AddHttpLogging(options =>
            {
                options.LoggingFields = HttpLoggingFields.All;
                options.CombineLogs = true;
            });
        }

        _ = builder.Services.AddJanus(
            "Host=unused",
            new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
            new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
            Encoding.UTF8.GetBytes("the secret this application presents"),
            HostFixture.Declaration(),
            ApplicationKind.Public);

        // The transport every host registers, which the delivery report reads the
        // gateway's parameters through.
        _ = builder.Services.AddSingleton<ISmsTransport, SmsTransportInMemory>();

        return builder.Build();
    }

    // The logging after routing, as the framework asks for per-endpoint settings.
    private static RequestDelegate Pipeline(WebApplication application)
    {
        _ = ((IApplicationBuilder)application).UseRouting();
        _ = ((IApplicationBuilder)application).UseHttpLogging();
        _ = ((IApplicationBuilder)application).UseEndpoints(_ => { });

        return ((IApplicationBuilder)application).Build();
    }

    private static async Task<int> PostAsync(WebApplication application, RequestDelegate pipeline, string path)
    {
        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();

        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        context.Request.Method = HttpMethods.Post;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("identity.example.test");
        context.Request.Path = path;
        context.Request.ContentType = "application/json";
        byte[] body = Encoding.UTF8.GetBytes(Body);

        context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyPresent());
        context.Request.ContentLength = body.Length;
        context.Request.Body = new MemoryStream(body);
        context.Response.Body = new MemoryStream();

        await pipeline(context);

        return context.Response.StatusCode;
    }

    private IEnumerable<string> Logged() =>
        _logs.Lines.Where(line => line.Contains("HttpLogging", StringComparison.Ordinal));
}
