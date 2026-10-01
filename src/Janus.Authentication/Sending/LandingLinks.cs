using System;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// The address a link the library sends opens: the landing origin of the application its
/// kind belongs to, then <c>/link#</c>, the kind, <c>.</c> and the token.
/// </summary>
/// <param name="origins">The origins the host declared.</param>
/// <remarks>
/// Implements API-LAND-001, FE-VER-001, LIB-HOST-001 and INT-SMS-003, chapter 10 sections
/// 5.26 and 5.43. The token travels in the fragment, which no request carries (RFC 9110),
/// so no server receives it in the address and a scanner that fetches the address
/// learns nothing a press would send.
/// </remarks>
internal sealed class LandingLinks(LandingOrigins origins)
{
    private const string Path = "/link#";

    /// <summary>
    /// The address one link opens.
    /// </summary>
    /// <param name="kind">What the link is for.</param>
    /// <param name="token">The token it carries.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentNullException">The token is absent.</exception>
    public string Of(LinkKind kind, [NeverLogged] string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return (LandsOnAuthentication(kind) ? origins.Authentication : origins.Account)
            + Path
            + WrittenName.Of(kind)
            + "."
            + token;
    }

    /// <summary>
    /// The widest address a link can be under these origins: the longer origin, the
    /// widest kind and a drawn token, one token size serving every link.
    /// </summary>
    /// <param name="origins">The origins the host declared.</param>
    /// <returns>The width.</returns>
    /// <exception cref="ArgumentNullException">The origins are absent.</exception>
    public static int Widest(LandingOrigins origins)
    {
        ArgumentNullException.ThrowIfNull(origins);

        return Math.Max(origins.Authentication.Length, origins.Account.Length)
            + Path.Length
            + Enum.GetValues<LinkKind>().Max(kind => WrittenName.Of(kind).Length)
            + 1
            + OpaqueToken.Width;
    }

    /// <summary>
    /// Whether a kind lands on the authentication application rather than the account
    /// application.
    /// </summary>
    /// <param name="kind">What the link is for.</param>
    /// <returns>Whether it does.</returns>
    public static bool LandsOnAuthentication(LinkKind kind) =>
        kind is LinkKind.SignIn
            or LinkKind.Registration
            or LinkKind.Recovery
            or LinkKind.Enrolment
            or LinkKind.Invitation;
}
