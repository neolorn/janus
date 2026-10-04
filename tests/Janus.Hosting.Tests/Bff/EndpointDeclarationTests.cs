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

    // The head of every namespace of the library's own types.
    private static readonly string Library = typeof(Result).Namespace!.Split('.')[0] + ".";

    private static readonly string[] Samples = ["3f2504e0-4f89-41d3-9a0c-0305e82c3301", "a"];

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment a browser can register against, so a request reaches the endpoint
    /// rather than the stage that asks for a session.
    /// </summary>
    public EndpointDeclarationTests() => Flow.Prepare(_deployment);

    /// <summary>
    /// CONV-DESIGN-006 AC3: every library endpoint carries a declaration, one that
    /// answers no code of its own included, so the contract file is generated from what
    /// each declares and from nothing a handler happens to answer.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_AC3_EveryEndpointCarriesADeclaration()
    {
        Assert.All(
            _deployment.Endpoints.OfType<RouteEndpoint>(),
            endpoint => Assert.True(
                EndpointDeclaration.Of(endpoint) is not null,
                $"{endpoint.RoutePattern.RawText} declares nothing."));
    }

    /// <summary>
    /// CONV-DESIGN-006 AC3: an endpoint declares the codes the text of its section gives
    /// the routes that text governs. Chapter 09 section 8 gives every route under
    /// <c>/admin</c> the missing permission, and every route whose path names an
    /// organization the organization the deployment does not hold; section 2 gives each
    /// step endpoint of a registration the step that is not the session's.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_AC3_AnEndpointDeclaresWhatItsSectionGivesIt()
    {
        string[] steps =
        [
            "PUT /register/age",
            "PUT /register/email",
            "PUT /register/phone",
            "POST /register/phone/skip",
            "POST /register/identifiers",
            "PUT /register/identifiers/{id}",
            "DELETE /register/identifiers/{id}",
            "POST /register/confirm",
            "PUT /register/security",
            "POST /register/terms",
        ];
        RouteEndpoint[] mounted = [.. _deployment.Endpoints.OfType<RouteEndpoint>()];

        foreach (RouteEndpoint endpoint in mounted)
        {
            string route = endpoint.RoutePattern.RawText!;
            IReadOnlyList<ErrorCode> declared = EndpointDeclaration.Of(endpoint)!.Codes;

            if (route.StartsWith("/admin/", StringComparison.Ordinal))
            {
                Assert.True(declared.Contains(ErrorCodes.Denied), $"{route} does not declare {ErrorCodes.Denied}.");
            }

            if (route.StartsWith("/admin/organizations/{id}", StringComparison.Ordinal))
            {
                Assert.True(
                    declared.Contains(ErrorCodes.OrganizationNotFound),
                    $"{route} does not declare {ErrorCodes.OrganizationNotFound}.");
            }
        }

        foreach (string step in steps)
        {
            RouteEndpoint endpoint = Assert.Single(mounted, candidate => Named(candidate) == step);

            Assert.Contains(ErrorCodes.RegistrationIncomplete, EndpointDeclaration.Of(endpoint)!.Codes);
        }
    }

    /// <summary>
    /// CONV-DESIGN-006: an endpoint declares a code once.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_AnEndpointDeclaresEachCodeOnce()
    {
        Assert.All(
            _deployment.Endpoints.OfType<RouteEndpoint>(),
            endpoint =>
            {
                IReadOnlyList<ErrorCode> declared = EndpointDeclaration.Of(endpoint)?.Codes ?? [];

                Assert.Equal(declared.Distinct(), declared);
            });
    }

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

    private static string Named(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods[0]
        + " "
        + endpoint.RoutePattern.RawText!.TrimEnd('/');

    private static IReadOnlyList<DeclaredValue> Declared(Endpoint endpoint) =>
        EndpointDeclaration.Of(endpoint)?.Values ?? [];

    // A typed value is one of the library's own types that reads itself from text.
    private static bool Typed(ParameterInfo parameter) =>
        parameter.ParameterType.Namespace?.StartsWith(Library, StringComparison.Ordinal) is true
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
