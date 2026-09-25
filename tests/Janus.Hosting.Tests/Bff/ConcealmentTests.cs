using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// Stage 11's concealment: a refusal the gate concealed is answered as the absence of
/// the record, whatever the endpoint wrote (AUTHZ-CONCEAL-004, BFF-ERR-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class ConcealmentTests
{
    private const string Traced = "0HN7TRACED:00000001";

    private static readonly AuditRecordId Recorded =
        new(Guid.Parse("01990a1c-7c00-7000-8000-00000000c0de"));

    // The logging every host registers, which the writer logs each refusal through
    // (BFF-LOG-001).
    private static readonly ServiceProvider Logging = new ServiceCollection().AddLogging().BuildServiceProvider();

    private readonly LogInMemory<Concealment> _log = new();

    /// <summary>
    /// BFF-ERR-003 AC1: one endpoint answers a record it may not show with a refusal
    /// of its own and a header naming it, another answers a missing one with a bare
    /// 404; under the same identifier the two leave as the same status, headers and
    /// bytes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_003_AC1_AConcealedRefusalIsTheSameBytesWhateverTheEndpointWroteAsync()
    {
        HttpContext present = await ConcealedAsync(async (context, refusals) =>
        {
            refusals.Concealed(Recorded);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers["X-Record-Owner"] = "someone";
            await context.Response.WriteAsJsonAsync(
                new { code = "authz.denied" },
                TestContext.Current.CancellationToken);
        });

        HttpContext absent = await ConcealedAsync((context, refusals) =>
        {
            refusals.Concealed(Recorded);
            context.Response.StatusCode = StatusCodes.Status404NotFound;

            return Task.CompletedTask;
        });

        Assert.Equal(StatusCodes.Status404NotFound, present.Response.StatusCode);
        Assert.Equal(absent.Response.StatusCode, present.Response.StatusCode);
        Assert.Equal(Headers(absent), Headers(present));
        Assert.Equal(Written(absent), Written(present));
        Assert.Equal("authz.resource.notfound", Answered(present).GetProperty("code").GetString());
    }

    /// <summary>
    /// AUTHZ-CONCEAL-004 AC1: the answer carries the identifier the refusal was recorded
    /// under, and the log ties it to the request.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_004_AC1_TheAnswerCarriesTheIdentifierTheRefusalWasRecordedUnderAsync()
    {
        HttpContext context = await ConcealedAsync((_, refusals) =>
        {
            refusals.Concealed(Recorded);

            return Task.CompletedTask;
        });

        JsonElement answered = Answered(context);

        Assert.Equal(Traced, answered.GetProperty("correlationId").GetString());
        Assert.Equal(Recorded.Value, answered.GetProperty("details").GetProperty("correlation").GetGuid());
        Assert.Contains((LogLevel.Information, 15), _log.Entries);
    }

    /// <summary>
    /// CONV-LOG-002 AC1: the identifier a concealed denial answers with resolves to
    /// the entries that request wrote, which name the refusal it was recorded as and
    /// the absence it was answered as.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_LOG_002_AC1_TheIdentifierInADenialResolvesToThatRequestsEntriesAsync()
    {
        using var logs = new LogsInMemory();

        await using ServiceProvider logging = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs))
            .BuildServiceProvider();

        HttpContext context = await ConcealedAsync(
            (_, refusals) =>
            {
                refusals.Concealed(Recorded);

                return Task.CompletedTask;
            },
            logging: logging);

        string answered = Answered(context).GetProperty("correlationId").GetString()!;
        string[] resolved = [.. logs.Lines.Where(line => line.Contains(answered, StringComparison.Ordinal))];

        Assert.Contains(resolved, line => line.Contains(Recorded.Value.ToString(), StringComparison.Ordinal));
        Assert.Contains(
            resolved,
            line => line.Contains(ErrorCodes.ResourceNotFound.ToString(), StringComparison.Ordinal));
        Assert.Equal(logs.Lines.Count, resolved.Length);
    }

    /// <summary>
    /// Where the endpoint refused more than once, the first refusal is the one
    /// answered, and the others stay in the trail.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task InvokeAsync_TwoRefusalsConcealed_AnswersTheFirstAsync()
    {
        HttpContext context = await ConcealedAsync((_, refusals) =>
        {
            refusals.Concealed(Recorded);
            refusals.Concealed(new AuditRecordId(Guid.Parse("01990a1c-7c00-7000-8000-000000000bad")));

            return Task.CompletedTask;
        });

        Assert.Equal(
            Recorded.Value,
            Answered(context).GetProperty("details").GetProperty("correlation").GetGuid());
    }

    /// <summary>
    /// BFF-ERR-003: what the response carried when the request reached the endpoints
    /// is kept, and what the endpoint added is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task InvokeAsync_ARefusalConcealed_KeepsWhatTheStagesWroteAsync()
    {
        HttpContext context = await ConcealedAsync((context, refusals) =>
        {
            refusals.Concealed(Recorded);
            context.Response.Headers["X-Record-Owner"] = "someone";

            return Task.CompletedTask;
        });

        Assert.Equal("stage=written; path=/", context.Response.Headers.SetCookie.ToString());
        Assert.False(context.Response.Headers.ContainsKey("X-Record-Owner"));
    }

    /// <summary>
    /// A refusal that was not concealed leaves the answer as the endpoint wrote it,
    /// which is how a disclosing type's refusal says the record is there.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task InvokeAsync_NothingConcealed_LeavesTheAnswerAsTheEndpointWroteItAsync()
    {
        HttpContext context = await ConcealedAsync(async (context, _) =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new { code = "authz.denied" },
                TestContext.Current.CancellationToken);
        });

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("authz.denied", Answered(context).GetProperty("code").GetString());
        Assert.Empty(_log.Entries);
    }

    /// <summary>
    /// BFF-ERR-003: an answer the endpoint had begun before the gate refused cannot be
    /// taken back, so the connection is broken off rather than finished, and the
    /// operator is told.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task InvokeAsync_AnAnswerBegunBeforeTheRefusal_IsBrokenOffAsync()
    {
        var lifetime = new Lifetime();

        HttpContext context = await ConcealedAsync(
            async (context, refusals) =>
            {
                await context.Response.Body.WriteAsync(
                    Encoding.UTF8.GetBytes("{\"title\":"),
                    TestContext.Current.CancellationToken);
                refusals.Concealed(Recorded);
                await context.Response.Body.WriteAsync(
                    Encoding.UTF8.GetBytes("\"Quarterly\"}"),
                    TestContext.Current.CancellationToken);
            },
            lifetime);

        Assert.True(lifetime.Aborted);
        Assert.Equal("{\"title\":", Written(context));
        Assert.Contains((LogLevel.Error, 16), _log.Entries);
    }

    private static string Headers(HttpContext context) => string.Join(
        '\n',
        context.Response.Headers
            .Select(header => header.Key + ": " + header.Value)
            .Order(StringComparer.Ordinal));

    private static string Written(HttpContext context) =>
        Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());

    private static JsonElement Answered(HttpContext context) =>
        JsonDocument.Parse(Written(context)).RootElement.Clone();

    // One request through the layer: the stages write a cookie, the endpoint runs,
    // and the layer answers. Given logging, the layer and the writer log through it.
    private async Task<HttpContext> ConcealedAsync(
        Func<HttpContext, ConcealedRefusals, Task> endpoint,
        Lifetime? lifetime = null,
        ServiceProvider? logging = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = logging ?? Logging,
            TraceIdentifier = Traced,
            Response = { Body = new MemoryStream() },
        };

        context.Features.Set<IHttpRequestLifetimeFeature>(lifetime ?? new Lifetime());

        var refusals = new ConcealedRefusals();

        ILogger<Concealment> log = logging?.GetRequiredService<ILogger<Concealment>>() ?? _log;

        await new Concealment(refusals, log).InvokeAsync(context, async reached =>
        {
            reached.Response.Headers.SetCookie = "stage=written; path=/";
            refusals.Reached(reached.Response.Headers);

            await endpoint(reached, refusals);
        });

        return context;
    }

    // Whether the request was broken off.
    private sealed class Lifetime : IHttpRequestLifetimeFeature
    {
        public bool Aborted { get; private set; }

        public CancellationToken RequestAborted { get; set; }

        public void Abort() => Aborted = true;
    }
}
