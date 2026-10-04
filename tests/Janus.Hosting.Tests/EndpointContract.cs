using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Tests;

/// <summary>
/// The HTTP endpoints as a contract: what the endpoint data source of a host that mounts
/// every library endpoint says of each one, written as the lines of the committed
/// contract file (LIB-API-001, CONV-DESIGN-006).
/// </summary>
internal static partial class EndpointContract
{
    private const string Member = "    ";

    private const string Line = "  ";

    private const string RetryAt = "retryAt";

    /// <summary>
    /// The lines of the contract: each endpoint by method and route pattern, then
    /// indented its request body's members, each status it answers with the members of
    /// the body it produces there and the codes the status carries, and last the browser
    /// profile's stages in order with the pipeline's own answers.
    /// </summary>
    /// <param name="endpoints">Every endpoint the host mounted.</param>
    /// <param name="json">The options a request body is read through.</param>
    /// <returns>The lines.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static IReadOnlyList<string> Lines(IReadOnlyList<Endpoint> endpoints, JsonSerializerOptions json)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(json);

        var lines = new List<string>();

        foreach ((string route, string method, RouteEndpoint endpoint) in endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => EndpointDeclaration.Of(endpoint) is not null)
            .SelectMany(Named)
            .OrderBy(named => named.Route, StringComparer.Ordinal)
            .ThenBy(named => named.Method, StringComparer.Ordinal))
        {
            lines.Add(method + " " + route);
            lines.AddRange(Request(endpoint, json));
            lines.AddRange(Answers(endpoint, json));
        }

        lines.Add("pipeline");
        lines.AddRange(Stages().Select(stage => Line + stage));
        lines.AddRange(Statuses([.. EndpointAnswers.Pipeline, ErrorCodes.ResourceNotFound])
            .OrderBy(status => status.Key)
            .Select(status => Line + Status(status.Key) + " " + string.Join(' ', status.Value)));

        return lines;
    }

    // An endpoint under each method it takes, by the path chapter 09 writes it at.
    private static IEnumerable<(string Route, string Method, RouteEndpoint Endpoint)> Named(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
            .Select(method => (endpoint.RoutePattern.RawText!.TrimEnd('/'), method, endpoint));

    private static IEnumerable<string> Request(RouteEndpoint endpoint, JsonSerializerOptions json)
    {
        if (endpoint.Metadata.GetMetadata<IAcceptsMetadata>() is not { RequestType: Type body } accepted)
        {
            yield break;
        }

        yield return Line + "request " + string.Join(' ', accepted.ContentTypes);

        foreach (string member in Members(body, string.Empty, json, []))
        {
            yield return Member + member;
        }
    }

    // Each status the endpoint answers: what it produces, then what it declares, what
    // its mounting answers and nothing else, each code under the status the status map
    // gives it.
    private static IEnumerable<string> Answers(RouteEndpoint endpoint, JsonSerializerOptions json)
    {
        IProducesResponseTypeMetadata[] produced = [.. endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()];
        SortedDictionary<int, SortedSet<string>> refused = Statuses(
            [.. EndpointDeclaration.Of(endpoint)!.Codes, .. EndpointAnswers.Mounting(endpoint)]);

        foreach (int status in produced.Select(result => result.StatusCode).Concat(refused.Keys).Distinct().Order())
        {
            yield return Line
                + Status(status)
                + (refused.TryGetValue(status, out SortedSet<string>? codes) ? " " + string.Join(' ', codes) : string.Empty);

            foreach (IProducesResponseTypeMetadata result in produced.Where(result => result.StatusCode == status))
            {
                if (result.Type is not Type body || body == typeof(void))
                {
                    continue;
                }

                yield return Line + Line + string.Join(' ', result.ContentTypes);

                foreach (string member in Members(body, string.Empty, json, []))
                {
                    yield return Member + Line + member;
                }
            }
        }
    }

    // A code under each status it can take: the status map gives a code one status,
    // save the code it maps by its details, which is listed under both.
    private static SortedDictionary<int, SortedSet<string>> Statuses(IReadOnlyList<ErrorCode> codes)
    {
        var statuses = new SortedDictionary<int, SortedSet<string>>();

        foreach (ErrorCode code in codes)
        {
            int[] taken =
            [
                ApiStatus.Of(Error.From(code)),
                ApiStatus.Of(Error.From(code, RetryAt, JsonSerializer.SerializeToElement(RetryAt))),
            ];

            foreach (int status in taken)
            {
                if (!statuses.TryGetValue(status, out SortedSet<string>? carried))
                {
                    statuses[status] = carried = new SortedSet<string>(StringComparer.Ordinal);
                }

                _ = carried.Add(code.ToString());
            }
        }

        return statuses;
    }

    // The members of a body by their JSON names, a member of a member after a full stop
    // and the elements of a list or a map after brackets or braces, each with the type
    // it is written as.
    private static IEnumerable<string> Members(Type type, string name, JsonSerializerOptions json, Type[] within)
    {
        Type read = Nullable.GetUnderlyingType(type) ?? type;
        string optional = read == type ? string.Empty : "?";

        if (within.Contains(read) || !json.TryGetTypeInfo(read, out JsonTypeInfo? shape))
        {
            return [name + ": " + Leaf(read) + optional];
        }

        return shape.Kind switch
        {
            JsonTypeInfoKind.Enumerable => Members(shape.ElementType!, name + "[]", json, within),
            JsonTypeInfoKind.Dictionary => Members(shape.ElementType!, name + "{}", json, within),
            JsonTypeInfoKind.Object when shape.Properties.Count > 0 => shape.Properties.SelectMany(property => Members(
                property.PropertyType,
                name.Length is 0 ? property.Name : name + "." + property.Name,
                json,
                [.. within, read])),
            _ => [name + ": " + Leaf(read) + optional],
        };
    }

    private static string Leaf(Type type) => Type.GetTypeCode(type) switch
    {
        _ when type.IsEnum => type.Name,
        TypeCode.String => "string",
        TypeCode.Boolean => "boolean",
        TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
            or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "number",
        _ => type.IsArray ? Leaf(type.GetElementType()!) + "[]" : type.Name,
    };

    private static string Status(int status) => status.ToString(CultureInfo.InvariantCulture);

    // The browser profile's stages in the order its mounting names them, which is the
    // order BFF-ORDER-001 fixes and the profile's own tests hold a request to.
    private static IEnumerable<string> Stages()
    {
        string profile = Repository.Source("PipelineProfiles");
        int from = profile.IndexOf("private static void Browser(", StringComparison.Ordinal);
        int to = profile.IndexOf("private static void Machine(", from, StringComparison.Ordinal);

        return Stage().Matches(profile[from..to]).Select(stage => stage.Groups[1].Value);
    }

    [GeneratedRegex("application\\.Use(?:Middleware<)?(\\w+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Stage();
}
