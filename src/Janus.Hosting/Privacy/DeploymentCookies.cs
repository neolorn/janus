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
/// <param name="hint">Which domain the start read the signed-in hint is kept on, where one is.</param>
/// <remarks>
/// Implements PRIV-CONS-006a and BFF-SESS-002 (D-203, D-279). The library sets no cookie
/// but the six BFF-SESS-002 names, so a host cookie under one of those names would be
/// listed twice and set over the library's, whether or not the deployment sets the
/// signed-in hint. Each library cookie is listed at the longest it lasts: the session and
/// CSRF cookies at <c>session.default.absolute</c>, the pre-authentication cookie ending
/// with the browser's session, the trusted-device cookie at
/// <c>factor.trusteddevice.lifetime</c>, the remembered-browser cookie at
/// <c>device.verification.lifetime</c>, and the signed-in hint, only while
/// <c>session.hint.domain</c> named a domain at the start, at
/// <c>session.default.absolute</c>, the longest an auth session lasts; no third party
/// reads any of them.
/// </remarks>
internal sealed class DeploymentCookies(
    IConfigurationStore configuration,
    AuthorizationDeclaration declaration,
    SignedInHintDomain hint)
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
        SignedInHint.Cookie,
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The keys that give the lifetime of a cookie the library sets.
    /// </summary>
    public static FrozenSet<ConfigurationKey> Lifetimes { get; } = new[]
    {
        Settings.SessionDefaultAbsolute.Key,
        Settings.FactorTrustedDeviceLifetime.Key,
        Settings.DeviceVerificationLifetime.Key,
    }.ToFrozenSet();

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

        // BFF-SESS-006 (D-279): the hint is set only while the domain the start read is
        // one, and the list answers to that same value, so it names the hint exactly when
        // it is set.
        bool hinted = hint.Domain is not null;

        return session.Match(
            absolute => device.Match(
                trusted => browser.Match(
                    remembered => Result.Success(Listed(absolute, trusted, remembered, hinted)),
                    Result.Failure<IReadOnlyList<NoticeCookie>>),
                Result.Failure<IReadOnlyList<NoticeCookie>>),
            Result.Failure<IReadOnlyList<NoticeCookie>>);
    }

    private IReadOnlyList<NoticeCookie> Listed(TimeSpan absolute, TimeSpan trusted, TimeSpan remembered, bool hinted) =>
    [
        .. new[]
        {
            new NoticeCookie(BrowserCookies.Session, Purpose: null, absolute, ThirdParty: false),
            new NoticeCookie(BrowserCookies.PreAuthentication, Purpose: null, Lifetime: null, ThirdParty: false),
            new NoticeCookie(BrowserCookies.Csrf, Purpose: null, absolute, ThirdParty: false),
            new NoticeCookie(BrowserCookies.Device, Purpose: null, trusted, ThirdParty: false),
            new NoticeCookie(BrowserCookies.Browser, Purpose: null, remembered, ThirdParty: false),
        }
            .Concat(hinted ? [new NoticeCookie(SignedInHint.Cookie, Purpose: null, absolute, ThirdParty: false)] : [])
            .Concat(declaration.Cookies.Select(cookie =>
                new NoticeCookie(cookie.Name, cookie.Purpose, cookie.Lifetime, cookie.ThirdPartyReadable)))
            .OrderBy(cookie => cookie.Name, StringComparer.Ordinal),
    ];
}
