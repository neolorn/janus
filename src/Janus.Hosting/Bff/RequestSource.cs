using System;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// The source a host's own endpoint passes an operation for the request, as the
/// library's own endpoints pass it.
/// </summary>
/// <remarks>
/// Implements BFF-HOST-004. The source is keyed as every count per source is: the
/// address the connection arrived on as the framework's trusted proxies resolve it, never
/// one a header claims; an IPv4-mapped address as its IPv4 address; an IPv6 address as
/// its /64, written <c>&lt;prefix&gt;/64</c>, since one host holds every address of its
/// subnet, save one whose first three bits are 000, which is keyed whole; and
/// <c>unknown</c> where the connection has no address.
/// </remarks>
public static class RequestSource
{
    /// <summary>
    /// The source of one request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static string Of(HttpRequest request) => RequestOrigin.Source(request);
}
