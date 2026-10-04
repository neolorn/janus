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
/// answered by its mounting or by the pipeline (CONV-DESIGN-006, chapter 09 preamble).
/// </summary>
internal static class EndpointAnswers
{
    // Chapter 09 serves the well-known documents at the site root, outside the mount,
    // where a password manager and an authenticator read them and the forgery layers
    // ask them for nothing.
    private const string SiteRoot = "/.well-known/";

    /// <summary>
    /// The answers the pipeline gives a request that reached an endpoint, which
    /// chapter 09 lists once beside the stage order and no endpoint declares.
    /// </summary>
    public static IReadOnlyList<ErrorCode> Pipeline { get; } = [ErrorCodes.Throttled, ErrorCodes.SystemFault];

    /// <summary>
    /// What an endpoint's mounting answers, derived as the chapter's preamble says: the
    /// malformed request where the endpoint reads a body or binds a typed value, the
    /// forgery layers' refusal where they apply, and the absent session where the
    /// endpoint requires one. A route of the machine profile that refuses a session
    /// cookie is answered that refusal by the profile.
    /// </summary>
    /// <param name="endpoint">The endpoint, as it is mounted.</param>
    /// <returns>The codes, each once.</returns>
    /// <exception cref="ArgumentNullException">The endpoint is absent.</exception>
    public static IReadOnlyList<ErrorCode> Mounting(RouteEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var codes = new List<ErrorCode>();
        PathString? machine = Machine(endpoint);

        if (endpoint.Metadata.GetMetadata<IAcceptsMetadata>() is not null
            || EndpointDeclaration.Of(endpoint) is { Values.Count: > 0 })
        {
            codes.Add(ErrorCodes.RequestMalformed);
        }

        if (SessionRequired.Asks(endpoint))
        {
            codes.Add(ErrorCodes.SessionExpired);
        }

        if (machine is PathString governed && !MachineRoutes.IgnoresCookie(governed))
        {
            codes.Add(ErrorCodes.Denied);
        }

        if (machine is null && !endpoint.RoutePattern.RawText!.StartsWith(SiteRoot, StringComparison.Ordinal))
        {
            codes.Add(ErrorCodes.SessionCsrfInvalid);
        }

        return codes;
    }

    /// <summary>
    /// Fails the running test where the response is a refusal carrying a code the
    /// endpoint the request reached does not declare and neither its mounting nor the
    /// pipeline answers. A request that reached no library endpoint is not judged.
    /// </summary>
    /// <param name="context">The request, answered.</param>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static void Hold(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.GetEndpoint() is not RouteEndpoint endpoint
            || EndpointDeclaration.Of(endpoint) is not EndpointDeclaration declared
            || context.Response.StatusCode < StatusCodes.Status400BadRequest
            || context.Response.ContentType?.StartsWith("application/json", StringComparison.Ordinal) is not true)
        {
            return;
        }

        string route = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods[0]
            + " "
            + endpoint.RoutePattern.RawText;

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
