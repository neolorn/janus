using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Privacy.Documents;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The cookies a deployment sets: the library's own and those the host declares.
/// </summary>
/// <param name="configuration">Where the lifetimes of the library's cookies are read.</param>
/// <param name="declaration">The cookies the host declared.</param>
/// <remarks>
/// Implements PRIV-CONS-006a and BFF-SESS-002 (D-203). The library sets no cookie but
/// the five BFF-SESS-002 names, so a host cookie under one of those names would be
/// listed twice and set over the library's. Each library cookie is listed at the
/// longest it lasts: the session and CSRF cookies at <c>session.default.absolute</c>,
/// the pre-authentication cookie ending with the browser's session, the trusted-device
/// cookie at <c>factor.trusteddevice.lifetime</c> and the remembered-browser cookie at
/// <c>device.verification.lifetime</c>; no third party reads any of them.
/// </remarks>
internal sealed class DeploymentCookies(IConfigurationStore configuration, AuthorizationDeclaration declaration)
    : IDeploymentCookies
{
    /// <summary>
    /// The names of the cookies the library sets.
    /// </summary>
    public static FrozenSet<string> Library { get; } = new[]
    {
        BrowserCookies.Session,
        BrowserCookies.PreAuthentication,
        BrowserCookies.Csrf,
        BrowserCookies.Device,
        BrowserCookies.Browser,
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<NoticeCookie>>> ReadAsync(CancellationToken cancellationToken)
    {
        Result<TimeSpan> session = await configuration
            .ReadAsync(Settings.SessionDefaultAbsolute, cancellationToken)
            .ConfigureAwait(false);
        Result<TimeSpan> device = await configuration
            .ReadAsync(Settings.FactorTrustedDeviceLifetime, cancellationToken)
            .ConfigureAwait(false);
        Result<TimeSpan> browser = await configuration
            .ReadAsync(Settings.DeviceVerificationLifetime, cancellationToken)
            .ConfigureAwait(false);

        return session.Match(
            absolute => device.Match(
                trusted => browser.Match(
                    remembered => Result.Success(Listed(absolute, trusted, remembered)),
                    Result.Failure<IReadOnlyList<NoticeCookie>>),
                Result.Failure<IReadOnlyList<NoticeCookie>>),
            Result.Failure<IReadOnlyList<NoticeCookie>>);
    }

    private IReadOnlyList<NoticeCookie> Listed(TimeSpan absolute, TimeSpan trusted, TimeSpan remembered) =>
    [
        .. new[]
        {
            new NoticeCookie(BrowserCookies.Session, Purpose: null, absolute, ThirdParty: false),
            new NoticeCookie(BrowserCookies.PreAuthentication, Purpose: null, Lifetime: null, ThirdParty: false),
            new NoticeCookie(BrowserCookies.Csrf, Purpose: null, absolute, ThirdParty: false),
            new NoticeCookie(BrowserCookies.Device, Purpose: null, trusted, ThirdParty: false),
            new NoticeCookie(BrowserCookies.Browser, Purpose: null, remembered, ThirdParty: false),
        }
            .Concat(declaration.Cookies.Select(cookie =>
                new NoticeCookie(cookie.Name, cookie.Purpose, cookie.Lifetime, cookie.ThirdPartyReadable)))
            .OrderBy(cookie => cookie.Name, StringComparer.Ordinal),
    ];
}
