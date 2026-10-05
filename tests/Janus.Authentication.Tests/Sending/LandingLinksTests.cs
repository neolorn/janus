using System;
using System.Linq;
using System.Security.Cryptography;
using Janus.Authentication.Sending;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The address a link the library sends opens, which names the application it lands on
/// and carries its token where no request does (API-LAND-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class LandingLinksTests
{
    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    private static readonly string[] OnAuthentication =
        ["sign-in", "registration", "recovery", "enrolment", "invitation"];

    /// <summary>
    /// API-LAND-001 AC4: every link is the declared origin of its kind's application,
    /// <c>/link#</c>, the kind, a full stop and the token, the first five kinds landing
    /// on the authentication application and the rest on the account application.
    /// </summary>
    [Fact]
    public void API_LAND_001_AC4_EveryLinkIsItsApplicationsOriginThenItsKindAndToken()
    {
        string token = OpaqueToken.Draw(Randomness).Value;

        Assert.All(
            Enum.GetValues<LinkKind>(),
            kind =>
            {
                string written = WrittenName.Of(kind);
                string origin = OnAuthentication.Contains(written, StringComparer.Ordinal)
                    ? Landing.Origins.Authentication
                    : Landing.Origins.Account;

                string link = Landing.Links.Of(kind, token);

                Assert.Equal(origin + "/link#" + written + "." + token, link);
                Assert.True(Uri.TryCreate(link, UriKind.Absolute, out Uri? address));
                Assert.Equal(origin, address!.GetLeftPart(UriPartial.Authority));
                Assert.Equal("#" + written + "." + token, address.Fragment);
                Assert.Equal(string.Empty, address.Query);
            });
    }

    /// <summary>
    /// API-LAND-001 AC4 (D-190): the <c>identifier</c> link of a replace an enrolment
    /// session staged is the one exception to the kind deciding the application: it
    /// carries the declared origin of the authentication application, where that
    /// session is held, and is otherwise the link every other is.
    /// </summary>
    [Fact]
    public void API_LAND_001_AC4_TheIdentifierLinkOfAnEnrolmentSessionsReplaceLandsOnAuthentication()
    {
        string token = OpaqueToken.Draw(Randomness).Value;

        string link = Landing.Links.OfEnrolmentReplace(token);

        Assert.Equal(Landing.Origins.Authentication + "/link#identifier." + token, link);
        Assert.Equal(Landing.Origins.Account + "/link#identifier." + token, Landing.Links.Of(LinkKind.Identifier, token));
        Assert.True(link.Length <= LandingLinks.Widest(Landing.Origins));
    }

    /// <summary>
    /// INT-SMS-003: a link is measured at the longer declared origin, the widest kind and
    /// a drawn token, so no link the library sends is wider than the budget allowed for.
    /// </summary>
    [Fact]
    public void INT_SMS_003_NoLinkIsWiderThanItIsMeasured()
    {
        string token = OpaqueToken.Draw(Randomness).Value;

        Assert.Equal(
            Landing.Origins.Authentication.Length + "/link#".Length + "identifier-confirm".Length + 1 + token.Length,
            LandingLinks.Widest(Landing.Origins));
        Assert.All(
            Enum.GetValues<LinkKind>(),
            kind => Assert.True(Landing.Links.Of(kind, token).Length <= LandingLinks.Widest(Landing.Origins)));
    }
}
