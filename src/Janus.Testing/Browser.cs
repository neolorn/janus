using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Testing;

/// <summary>
/// One browser at a host's application: it keeps the cookies the application sets and
/// sends them back, and sets on every request to the application's origin what the
/// first-party frontend and the browser set, so a request passes the browser profile's
/// CSRF stages as the frontend's does.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003, BFF-CSRF-001 to BFF-CSRF-004 and BFF-SESS-002. It sends the
/// custom request header, the synchronizer token the token cookie holds, the origin the
/// request is made from and the fetch metadata a same-origin request carries; a header
/// the request already carries is left as it is, so a test can send one of its own. The
/// library deletes a cookie by writing it empty, so an empty cookie is forgotten. A
/// request to another origin is passed on untouched, carrying nothing of this one.
/// </remarks>
internal sealed class Browser : DelegatingHandler
{
    // BFF-CSRF-003: the cookie the synchronizer token is read from by the frontend.
    private const string TokenCookie = "__Host-identity-csrf";

    // BFF-CSRF-003: the custom request header, whose presence alone is checked.
    private const string RequestHeader = "X-Identity-Request";

    // BFF-CSRF-003: the header the synchronizer token is presented in.
    private const string TokenHeader = "X-Identity-Csrf";

    // BFF-CSRF-002: the fetch metadata a browser sets on a request a page of the same
    // origin makes.
    private const string FetchSite = "Sec-Fetch-Site";

    private const string SameOrigin = "same-origin";

    private readonly Lock _held = new();

    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    private readonly string _origin;

    /// <summary>
    /// Makes a browser that holds nothing yet.
    /// </summary>
    /// <param name="address">Where the application is served; its origin is the one the browser is at.</param>
    /// <param name="channel">What carries a request to the application.</param>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public Browser(Uri address, HttpMessageHandler channel)
        : base(channel)
    {
        ArgumentNullException.ThrowIfNull(address);

        _origin = address.GetLeftPart(UriPartial.Authority);
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool here = request.RequestUri is { IsAbsoluteUri: true } target
            && string.Equals(target.GetLeftPart(UriPartial.Authority), _origin, StringComparison.OrdinalIgnoreCase);

        if (here)
        {
            Carry(request);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (here)
        {
            Keep(response);
        }

        return response;
    }

    private void Carry(HttpRequestMessage request)
    {
        string? token;
        string cookies;

        lock (_held)
        {
            token = _cookies.GetValueOrDefault(TokenCookie);
            cookies = string.Join("; ", _cookies.Select(cookie => cookie.Key + "=" + cookie.Value));
        }

        if (cookies.Length is not 0 && !request.Headers.Contains("Cookie"))
        {
            _ = request.Headers.TryAddWithoutValidation("Cookie", cookies);
        }

        Set(request, RequestHeader, "1");
        Set(request, "Origin", _origin);
        Set(request, FetchSite, SameOrigin);

        if (token is not null)
        {
            Set(request, TokenHeader, token);
        }
    }

    private static void Set(HttpRequestMessage request, string header, string value)
    {
        if (!request.Headers.Contains(header))
        {
            _ = request.Headers.TryAddWithoutValidation(header, value);
        }
    }

    private void Keep(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? written))
        {
            return;
        }

        lock (_held)
        {
            foreach (string header in written)
            {
                string pair = header.Split(';', 2)[0];
                int at = pair.IndexOf('=', StringComparison.Ordinal);

                if (at <= 0)
                {
                    continue;
                }

                string name = pair[..at].Trim();
                string value = pair[(at + 1)..].Trim();

                if (value.Length is 0)
                {
                    _ = _cookies.Remove(name);
                }
                else
                {
                    _cookies[name] = value;
                }
            }
        }
    }
}
