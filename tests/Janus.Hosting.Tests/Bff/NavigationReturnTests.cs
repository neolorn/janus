using System;
using System.Text.Json;
using Janus.Core;
using Janus.Hosting.Bff;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// Where a navigation route returns the browser it refused (BFF-ERR-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class NavigationReturnTests
{
    private static readonly DateTimeOffset Lifts = new(2026, 3, 1, 12, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// BFF-ERR-001 AC4: the code crosses in the query member <c>error</c>, after any
    /// query the destination already has and before any fragment.
    /// </summary>
    /// <param name="destination">Where the browser started from.</param>
    /// <param name="returned">Where it is returned to.</param>
    [Theory]
    [InlineData("/", "/?error=auth.session.expired")]
    [InlineData("/account/profile", "/account/profile?error=auth.session.expired")]
    [InlineData("/account?tab=security", "/account?tab=security&error=auth.session.expired")]
    [InlineData("/account#keys", "/account?error=auth.session.expired#keys")]
    [InlineData("/account?tab=security#keys", "/account?tab=security&error=auth.session.expired#keys")]
    public void BFF_ERR_001_AC4_TheCodeIsPlacedBeforeAnyFragment(string destination, string returned) =>
        Assert.Equal(returned, NavigationReturn.Refused(destination, Error.From(ErrorCodes.SessionExpired)));

    /// <summary>
    /// BFF-ERR-001 AC4 and BFF-ABUSE-001: a refusal naming the instant its wait lifts
    /// carries that instant beside the code, written as the wire writes every instant,
    /// whether it is throttled or a send a restriction refused.
    /// </summary>
    [Fact]
    public void BFF_ERR_001_AC4_ARefusalNamingItsInstantCarriesItBesideTheCode()
    {
        string instant = Uri.EscapeDataString(JsonSerializer.SerializeToElement(Lifts).GetString()!);
        var restricted = Error.From(ErrorCodes.RestrictionExceeded, "retryAt", JsonSerializer.SerializeToElement(Lifts));

        Assert.Equal(
            "/account?error=auth.throttled&retryAt=" + instant + "#keys",
            NavigationReturn.Refused("/account#keys", Error.Throttled(Lifts)));
        Assert.Equal(
            "/register?error=auth.restriction.exceeded&retryAt=" + instant,
            NavigationReturn.Refused("/register", restricted));
    }

    /// <summary>
    /// BFF-ERR-001 AC4: nothing else of a refusal's details crosses.
    /// </summary>
    [Fact]
    public void BFF_ERR_001_AC4_NothingElseOfTheDetailsCrosses()
    {
        var refusal = Error.From(ErrorCodes.StepUpRequired, "action", JsonSerializer.SerializeToElement("provider:link"));

        Assert.Equal("/account?error=auth.stepup.required", NavigationReturn.Refused("/account", refusal));
    }
}
