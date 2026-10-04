using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Janus.Authentication.Sessions;
using Janus.Core;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// Where a request came from, in the terms a session records.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-001, AUTH-SESS-013, AUTH-ABUSE-001 and INT-GEN-006. The address
/// is the one the connection arrived on, never one a header claims: a deployment behind
/// a proxy tells the framework which proxies it trusts, and nothing here second-guesses
/// that. A session records the whole address; every count per source is kept by the
/// source, which for an IPv6 address is its /64, since one host holds every address of
/// its subnet (RFC 4291, RFC 8981).
/// The description is the two coarse facts a person recognises their own device by
/// and no more; anything finer would be a fingerprint, which is not what the list is
/// for. Where the request was is not read here at all: the library resolves that from
/// the address when it records the session (INT-GEN-006).
/// </remarks>
internal static class RequestOrigin
{
    private const string Unknown = "unknown";

    private const int SubnetBits = 64;

    private const int SiteBits = 48;

    /// <summary>
    /// Reads where one request came from.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The origin.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static SessionOrigin Of(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string agent = request.Headers.UserAgent.ToString();

        return new SessionOrigin(
            Address(request),
            new DeviceDescription(Browser(agent), System(agent)))
        {
            Source = Source(request),
        };
    }

    /// <summary>
    /// The whole address one request came from, which a session and a credential
    /// record (AUTH-SESS-013): an IPv4-mapped IPv6 address is read as its IPv4 address.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The address, or <c>unknown</c> where the connection has none.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static string Address(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Connected(request)?.ToString() ?? Unknown;
    }

    /// <summary>
    /// Where one request came from, for the counters that are kept per source
    /// (AUTH-ABUSE-001, AUTH-ABUSE-008): an IPv4 address as written; the /64 of an IPv6
    /// address, written <c>&lt;prefix&gt;/64</c>; an IPv6 address whose first three bits
    /// are 000, whole; a connection with no address, <c>unknown</c>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static string Source(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Connected(request) is not IPAddress address)
        {
            return Unknown;
        }

        return address.AddressFamily is AddressFamily.InterNetworkV6 && (address.GetAddressBytes()[0] >> 5) is not 0
            ? Prefix(address, SubnetBits)
            : address.ToString();
    }

    /// <summary>
    /// The /48 that encloses an IPv6 source, written <c>&lt;prefix&gt;/48</c>, which the
    /// flood limit also counts by, since one site holds a /48 or a /56 (RFC 6177).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The site, or nothing where the source is not an IPv6 address.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static string? Site(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Connected(request) is { AddressFamily: AddressFamily.InterNetworkV6 } address
            ? Prefix(address, SiteBits)
            : null;
    }

    /// <summary>
    /// The language the person is reading in, which is the first tag of the header
    /// and nothing the account holds (CONV-CONTENT-001).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The tag, or nothing where the header carried none.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static string Language(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string accepted = request.Headers.AcceptLanguage.ToString();
        int ends = accepted.IndexOfAny([',', ';']);

        return (ends < 0 ? accepted : accepted[..ends]).Trim();
    }

    // The connection's address as every reading here takes it: an IPv4 address that
    // arrived mapped into IPv6 is that IPv4 address.
    private static IPAddress? Connected(HttpRequest request) =>
        request.HttpContext.Connection.RemoteIpAddress is { } address
            ? address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address
            : null;

    // The first bits of an IPv6 address with the rest zeroed, written as its prefix.
    private static string Prefix(IPAddress address, int bits)
    {
        byte[] kept = address.GetAddressBytes();

        Array.Clear(kept, bits / 8, kept.Length - (bits / 8));

        return string.Create(CultureInfo.InvariantCulture, $"{new IPAddress(kept)}/{bits}");
    }

    // Order matters: the engines that name themselves after the ones they replaced
    // come first, so a browser is called what its user calls it.
    private static string Browser(string agent) =>
        Named(agent, "Edg/", "Edge")
        ?? Named(agent, "OPR/", "Opera")
        ?? Named(agent, "Firefox/", "Firefox")
        ?? Named(agent, "Chrome/", "Chrome")
        ?? Named(agent, "Safari/", "Safari")
        ?? Unknown;

    private static string System(string agent) =>
        Named(agent, "Windows", "Windows")
        ?? Named(agent, "Android", "Android")
        ?? Named(agent, "iPhone", "iOS")
        ?? Named(agent, "iPad", "iPadOS")
        ?? Named(agent, "Mac OS X", "macOS")
        ?? Named(agent, "Linux", "Linux")
        ?? Unknown;

    private static string? Named(string agent, string token, string name) =>
        agent.Contains(token, StringComparison.Ordinal) ? name : null;
}
