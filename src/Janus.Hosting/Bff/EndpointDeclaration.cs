using System;
using System.Collections.Generic;
using System.Globalization;
using Janus.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Janus.Hosting.Bff;

/// <summary>
/// What an endpoint declares where it is mounted: the error codes chapter 09 gives it,
/// those it gives a navigation route to carry in its redirect, and the route and query
/// values it binds to a type.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006 and API-CONV-003. The codes are its row's, those the text
/// of its section gives the routes that text governs, and the gateway floor's where the
/// chapter's preamble gives it. What its mounting answers, where its row does not give
/// it, is derived and declared by no endpoint. It declares codes only, the status of
/// each being the one <see cref="ApiStatus"/> maps. A navigation route (BFF-ERR-001)
/// declares the same way the codes it carries in the query member <c>error</c> of its
/// redirect, which go with that redirect and with no status. The values are declared
/// in the order the request is read for the first that does not parse, and each
/// declaration is generic over the value's type, so no reflection reads the handler
/// (CONV-CODE-004).
/// </remarks>
internal sealed class EndpointDeclaration
{
    private EndpointDeclaration(
        IReadOnlyList<ErrorCode> codes,
        IReadOnlyList<ErrorCode> carried,
        IReadOnlyList<DeclaredValue> values)
    {
        Codes = codes;
        Carried = carried;
        Values = values;
    }

    /// <summary>
    /// The codes chapter 09 gives the endpoint, in the order declared.
    /// </summary>
    public IReadOnlyList<ErrorCode> Codes { get; }

    /// <summary>
    /// The codes chapter 09 gives a navigation route to carry in the query member
    /// <c>error</c> of its redirect, in the order declared.
    /// </summary>
    public IReadOnlyList<ErrorCode> Carried { get; }

    /// <summary>
    /// The typed route and query values the endpoint binds, in the order declared.
    /// </summary>
    public IReadOnlyList<DeclaredValue> Values { get; }

    /// <summary>
    /// Declares the codes chapter 09 gives an endpoint.
    /// </summary>
    /// <param name="codes">The codes.</param>
    /// <returns>The declaration, binding no value yet.</returns>
    /// <exception cref="ArgumentNullException">The codes are absent.</exception>
    public static EndpointDeclaration Answering(params ErrorCode[] codes)
    {
        ArgumentNullException.ThrowIfNull(codes);

        return new EndpointDeclaration([.. codes], [], []);
    }

    /// <summary>
    /// Declares the codes chapter 09 gives a navigation route to carry in the query
    /// member <c>error</c> of its redirect.
    /// </summary>
    /// <param name="codes">The codes.</param>
    /// <returns>The declaration with the codes after those it already carries.</returns>
    /// <exception cref="ArgumentNullException">The codes are absent.</exception>
    public EndpointDeclaration Carrying(params ErrorCode[] codes)
    {
        ArgumentNullException.ThrowIfNull(codes);

        return new EndpointDeclaration(Codes, [.. Carried, .. codes], Values);
    }

    /// <summary>
    /// What the endpoint the request reached declares.
    /// </summary>
    /// <param name="endpoint">The endpoint, or nothing where routing matched none.</param>
    /// <returns>The declaration, or nothing where there is none.</returns>
    public static EndpointDeclaration? Of(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<EndpointDeclaration>();

    /// <summary>
    /// Declares one more route or query value the endpoint binds to a type.
    /// </summary>
    /// <typeparam name="TValue">The type the handler takes the value as.</typeparam>
    /// <param name="name">The name the route pattern or the query gives it.</param>
    /// <returns>The declaration with the value after those already declared.</returns>
    /// <exception cref="ArgumentException">The name is absent or blank.</exception>
    public EndpointDeclaration Binding<TValue>(string name)
        where TValue : IParsable<TValue>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new EndpointDeclaration(
            Codes,
            Carried,
            [
                .. Values,
                new DeclaredValue(
                    name,
                    typeof(TValue),
                    text => TValue.TryParse(text, CultureInfo.InvariantCulture, out _)),
            ]);
    }

    /// <summary>
    /// The first declared value the request's text does not read as.
    /// </summary>
    /// <param name="request">The request the framework could not bind.</param>
    /// <returns>Its name, or nothing where every declared value reads.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public string? Unread(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (DeclaredValue value in Values)
        {
            // A route value is the pattern's own; one the pattern does not name is the
            // query's, and an absent one is no text at all.
            string? text = request.RouteValues.TryGetValue(value.Name, out object? routed)
                ? routed as string
                : request.Query.TryGetValue(value.Name, out StringValues asked)
                    ? asked.ToString()
                    : null;

            if (!value.Reads(text))
            {
                return value.Name;
            }
        }

        return null;
    }
}
