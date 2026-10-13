using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// What an endpoint answers beyond what it declares, and the check the test host runs
/// on every response it carries: no endpoint answers a code it neither declares nor is
/// answered by its mounting or by the pipeline, and no navigation route's redirect
/// carries a code the route does not declare it carries (CONV-DESIGN-006, chapter 09
/// preamble).
/// </summary>
internal static class EndpointAnswers
{
    // Chapter 09 serves the well-known documents at the site root, outside the mount,
    // where a password manager and an authenticator read them and the forgery layers
    // ask them for nothing.
    private const string SiteRoot = "/.well-known/";

    // BFF-ERR-001: the query member a navigation's redirect carries a refusal's code in.
    private const string Carrier = "error";

    /// <summary>
    /// The answers the pipeline gives a request that reached an endpoint, which
    /// chapter 09 lists once beside the stage order and no endpoint declares.
    /// </summary>
    public static IReadOnlyList<ErrorCode> Pipeline { get; } = [ErrorCodes.Throttled, ErrorCodes.SystemFault];

    /// <summary>
    /// What an endpoint's mounting answers, derived as the chapter's preamble says: the
    /// malformed request where the endpoint reads a body or binds a typed value, the
    /// forgery layers' refusal where they apply, and the absent session where the
    /// endpoint requires one. The machine profile derives none: a callback it refuses
    /// for carrying a session cookie is answered the code chapter 09 section 10 gives
    /// every callback's refusal, which the callback's endpoint declares, and a protocol
    /// endpoint of the provider answers in its protocol's shape, which carries no code
    /// (BFF-MACH-001).
    /// </summary>
    /// <param name="endpoint">The endpoint, as it is mounted.</param>
    /// <returns>The codes, each once.</returns>
    /// <exception cref="ArgumentNullException">The endpoint is absent.</exception>
    public static IReadOnlyList<ErrorCode> Mounting(RouteEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var codes = new List<ErrorCode>();

        if (endpoint.Metadata.GetMetadata<IAcceptsMetadata>() is not null
            || EndpointDeclaration.Of(endpoint) is { Values.Count: > 0 })
        {
            codes.Add(ErrorCodes.RequestMalformed);
        }

        if (SessionRequired.Asks(endpoint))
        {
            codes.Add(ErrorCodes.SessionExpired);
        }

        if (Machine(endpoint) is null && !endpoint.RoutePattern.RawText!.StartsWith(SiteRoot, StringComparison.Ordinal))
        {
            codes.Add(ErrorCodes.SessionCsrfInvalid);
        }

        return codes;
    }

    /// <summary>
    /// Fails the running test where the response is a refusal carrying a code the
    /// endpoint the request reached does not declare and neither its mounting nor the
    /// pipeline answers, or a redirect whose query member carries a code the endpoint
    /// does not declare it carries: a navigation route binds no typed value, so its
    /// mounting gives it none to carry. A request that reached no library endpoint is
    /// not judged, and neither is the redirect of a machine callback, which carries a
    /// provider's own member on.
    /// </summary>
    /// <param name="context">The request, answered.</param>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static void Hold(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.GetEndpoint() is not RouteEndpoint endpoint
            || EndpointDeclaration.Of(endpoint) is not EndpointDeclaration declared)
        {
            return;
        }

        string route = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods[0]
            + " "
            + endpoint.RoutePattern.RawText;

        if (context.Response.StatusCode is >= StatusCodes.Status300MultipleChoices and < StatusCodes.Status400BadRequest)
        {
            if (Machine(endpoint) is null
                && Borne(context.Response.Headers.Location.ToString()) is string borne
                && !declared.Carried.Any(known => known.ToString() == borne))
            {
                Assert.Fail($"{route} returned the browser with {Carrier}={borne}, which it does not declare.");
            }

            return;
        }

        if (context.Response.StatusCode < StatusCodes.Status400BadRequest
            || context.Response.ContentType?.StartsWith("application/json", StringComparison.Ordinal) is not true)
        {
            return;
        }

        if (Written(context.Response.Body) is not string body)
        {
            Assert.Fail($"{route} refused {context.Response.StatusCode} into a body the host cannot read.");

            return;
        }

        if (Code(body) is string code
            && !declared.Codes.Concat(Mounting(endpoint)).Concat(Pipeline).Any(known => known.ToString() == code))
        {
            Assert.Fail($"{route} answered {context.Response.StatusCode} {code}, which it does not declare.");
        }
    }

    // The code a redirect carries in its query member, which stands before any
    // fragment; an address with no such member carries none.
    private static string? Borne(string location)
    {
        int fragment = location.IndexOf('#', StringComparison.Ordinal);
        string address = fragment < 0 ? location : location[..fragment];
        int query = address.IndexOf('?', StringComparison.Ordinal);

        if (query < 0)
        {
            return null;
        }

        foreach (string member in address[(query + 1)..].Split('&'))
        {
            if (member.StartsWith(Carrier + "=", StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(member[(Carrier.Length + 1)..]);
            }
        }

        return null;
    }

    // The machine path a route pattern takes, where the machine profile governs one.
    private static PathString? Machine(RouteEndpoint endpoint)
    {
        string[] segments = endpoint.RoutePattern.RawText!.Split('/');

        foreach (PathString governed in MachineRoutes.Paths)
        {
            string[] taken = governed.Value!.Split('/');

            if (taken.Length == segments.Length
                && taken.Zip(segments).All(pair => pair.Second.StartsWith('{')
                    || string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase)))
            {
                return governed;
            }
        }

        return null;
    }

    private static string? Written(Stream body) => body switch
    {
        ResponseBody written => written.Taken(),
        MemoryStream written => Encoding.UTF8.GetString(written.ToArray()),
        _ => null,
    };

    // The code a refusal carries. A body that is not the library's error shape, as a
    // provider event's answer under RFC 8935 is not, carries none.
    private static string? Code(string body)
    {
        if (body.Length is 0)
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);

        return document.RootElement.ValueKind is JsonValueKind.Object
            && document.RootElement.TryGetProperty("code", out JsonElement code)
            && code.ValueKind is JsonValueKind.String
            ? code.GetString()
            : null;
    }
}
