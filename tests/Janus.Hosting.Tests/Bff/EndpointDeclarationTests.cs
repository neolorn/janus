using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// What an endpoint declares where it is mounted, and what the error translation
/// stage answers a request the framework could not bind to it (CONV-DESIGN-006,
/// API-CONV-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class EndpointDeclarationTests : IAsyncDisposable
{
    // Text no typed value reads: no identifier, no name and no key holds the symbol.
    private const string Unreadable = "~";

    private static readonly string[] Samples = ["3f2504e0-4f89-41d3-9a0c-0305e82c3301", "a"];

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment a browser can register against, so a request reaches the endpoint
    /// rather than the stage that asks for a session.
    /// </summary>
    public EndpointDeclarationTests() => Flow.Prepare(_deployment);

    /// <summary>
    /// CONV-DESIGN-006 AC5: each handler's typed route and query parameters equal those
    /// its endpoint declares, by name and type and in the handler's order, and no
    /// handler takes one as a bare <see cref="Guid"/>.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_AC5_EachHandlersTypedValuesAreTheOnesItsEndpointDeclares()
    {
        foreach (RouteEndpoint endpoint in _deployment.Endpoints.OfType<RouteEndpoint>())
        {
            ParameterInfo[] taken = endpoint.Metadata.GetMetadata<MethodInfo>()?.GetParameters() ?? [];

            Assert.DoesNotContain(taken, parameter => parameter.ParameterType == typeof(Guid));
            Assert.Equal(
                taken.Where(Typed).Select(parameter => (parameter.Name, parameter.ParameterType)),
                Declared(endpoint).Select(value => ((string?)value.Name, value.Type)));
        }
    }

    /// <summary>
    /// CONV-DESIGN-006 AC5 and API-CONV-003: a route or query value that does not parse
    /// as its type answers 400 <c>api.request.malformed</c> naming it, on every endpoint
    /// that declares one and for each value it declares.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_006_AC5_AValueThatDoesNotParseIsRefusedNamingItAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        int asked = 0;

        foreach (RouteEndpoint endpoint in _deployment.Endpoints.OfType<RouteEndpoint>())
        {
            IReadOnlyList<DeclaredValue> declared = Declared(endpoint);

            for (int unread = 0; unread < declared.Count; unread++)
            {
                Answer answered = await AskedAsync(browser, endpoint, declared, unread, unread + 1);

                Assert.True(
                    answered.Status == StatusCodes.Status400BadRequest,
                    $"{endpoint.RoutePattern.RawText} answered {answered.Status} for {declared[unread].Name}.");
                Assert.Equal(ErrorCodes.RequestMalformed.ToString(), answered.Text("code"));
                Assert.Equal(
                    declared[unread].Name,
                    answered.Json().GetProperty("details").GetProperty("member").GetString());

                asked++;
            }
        }

        Assert.NotEqual(0, asked);
    }

    /// <summary>
    /// CONV-DESIGN-006: where more than one declared value does not parse, the refusal
    /// names the first in the order declared.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_006_TheFirstDeclaredValueThatDoesNotParseIsTheOneNamedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        int asked = 0;

        foreach (RouteEndpoint endpoint in _deployment.Endpoints.OfType<RouteEndpoint>())
        {
            IReadOnlyList<DeclaredValue> declared = Declared(endpoint);

            if (declared.Count < 2)
            {
                continue;
            }

            Answer answered = await AskedAsync(browser, endpoint, declared, 0, declared.Count);

            Assert.Equal(StatusCodes.Status400BadRequest, answered.Status);
            Assert.Equal(
                declared[0].Name,
                answered.Json().GetProperty("details").GetProperty("member").GetString());

            asked++;
        }

        Assert.NotEqual(0, asked);
    }

    /// <summary>
    /// CONV-DESIGN-006: a body the reader cannot turn into the endpoint's is still named
    /// by its member where every declared value reads.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_006_ABodyIsNamedWhereEveryDeclaredValueReadsAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer answered = await browser.SendAsync(
            "PATCH",
            "/account/credentials/" + Samples[0],
            "{ \"label\": 7 }");

        Assert.Equal(StatusCodes.Status400BadRequest, answered.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), answered.Text("code"));
        Assert.Equal("label", answered.Json().GetProperty("details").GetProperty("member").GetString());
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _deployment.DisposeAsync();

    private static IReadOnlyList<DeclaredValue> Declared(Endpoint endpoint) =>
        EndpointDeclaration.Of(endpoint)?.Values ?? [];

    // A typed value is one of the library's own types that reads itself from text.
    private static bool Typed(ParameterInfo parameter) =>
        parameter.ParameterType.Namespace?.StartsWith("Janus.", StringComparison.Ordinal) is true
        && parameter.ParameterType
            .GetInterfaces()
            .Any(held => held.IsGenericType
                && held.GetGenericTypeDefinition() == typeof(IParsable<>)
                && held.GetGenericArguments()[0] == parameter.ParameterType);

    // Sends the endpoint a request in which the declared values from one position up
    // to another do not read and every other one does: a route value carries text no
    // type reads, and a query value is left out.
    private static Task<Answer> AskedAsync(
        Browser browser,
        RouteEndpoint endpoint,
        IReadOnlyList<DeclaredValue> declared,
        int from,
        int to)
    {
        var routed = new HashSet<string>(
            endpoint.RoutePattern.Parameters.Select(parameter => parameter.Name),
            StringComparer.Ordinal);
        string path = endpoint.RoutePattern.RawText!;

        foreach (string name in routed)
        {
            int at = Index(declared, name);
            string text = at < 0 ? "a" : at >= from && at < to ? Unreadable : Sample(declared[at]);

            path = path.Replace("{" + name + "}", text, StringComparison.Ordinal);
        }

        string query = string.Join(
            '&',
            declared
                .Where((value, at) => !routed.Contains(value.Name) && (at < from || at >= to))
                .Select(value => value.Name + "=" + Sample(value)));
        string method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods[0];

        return browser.SendAsync(
            method,
            query.Length is 0 ? path : path + "?" + query,
            HttpMethods.IsGet(method) ? null : "{}");
    }

    private static int Index(IReadOnlyList<DeclaredValue> declared, string name)
    {
        for (int at = 0; at < declared.Count; at++)
        {
            if (string.Equals(declared[at].Name, name, StringComparison.Ordinal))
            {
                return at;
            }
        }

        return -1;
    }

    private static string Sample(DeclaredValue value) => Samples.First(value.Reads);
}
