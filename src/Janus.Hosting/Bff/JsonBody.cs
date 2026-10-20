using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// The JSON body a library endpoint reads, read through the library context that
/// declares its type and never through the application's JSON options.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006 and CONV-DESIGN-007 (D-206). A member is read only under
/// the name <c>09</c> spells, case-sensitively, and a number only as a JSON number. A
/// body that does not read is refused as the framework refuses one it binds, so
/// <see cref="MalformedRequest"/> answers it 400 <c>api.request.malformed</c> naming the
/// member the reader stopped at. The framework reads no body for such an endpoint, so
/// the endpoint declares the body's type where it is mapped, and the endpoints'
/// contract still names its members (LIB-TEST-002).
/// </remarks>
internal static class JsonBody
{
    /// <summary>
    /// Reads the body.
    /// </summary>
    /// <typeparam name="TBody">What the body reads as.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The body.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    /// <exception cref="BadHttpRequestException">The body is not JSON, does not read, or is JSON <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">No library context declares the body's type.</exception>
    public static async Task<TBody> ReadAsync<TBody>(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        JsonSerializerContext declaring = LibraryContexts.Declaring(typeof(TBody))
            ?? throw new InvalidOperationException(typeof(TBody).Name + " is declared in no library context.");

        if (!request.HasJsonContentType())
        {
            throw new BadHttpRequestException(
                "The request body is not JSON.",
                StatusCodes.Status415UnsupportedMediaType);
        }

        object? value;

        try
        {
            value = await request
                .ReadFromJsonAsync(typeof(TBody), declaring, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException unreadable)
        {
            throw new BadHttpRequestException(
                "The request body does not read.",
                StatusCodes.Status400BadRequest,
                unreadable);
        }
        catch (IOException unreadable) when (unreadable is not BadHttpRequestException)
        {
            throw new BadHttpRequestException(
                "The request body could not be read.",
                StatusCodes.Status400BadRequest,
                unreadable);
        }

        return value is TBody body
            ? body
            : throw new BadHttpRequestException("The request body is empty.", StatusCodes.Status400BadRequest);
    }
}
