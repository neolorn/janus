using Janus.Hosting.Bff;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// Which destination a request named is followed, and what stands in for any other
/// (API-REDIR-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class ReturnDestinationTests
{
    /// <summary>
    /// API-REDIR-001: one path of this application is followed as it is, with its
    /// escapes, its query and its fragment, and so is the root.
    /// </summary>
    /// <param name="destination">The destination, as the route reads it.</param>
    [Theory]
    [InlineData("/")]
    [InlineData("/account/profile")]
    [InlineData("/account?tab=security#keys")]
    [InlineData("/orders/caf%C3%A9%20menu?tab=open%2Fpaid#notes")]
    [InlineData("/orders//42")]
    [InlineData("/!~")]
    [InlineData("/?next=https://attacker.example/")]
    public void API_REDIR_001_OnePathOfThisApplicationIsFollowedAsItIs(string destination) =>
        Assert.Equal(destination, ReturnDestination.Followed(destination));

    /// <summary>
    /// API-REDIR-001 AC1: a destination that is not one path of this application, and
    /// a request naming none, is replaced with the root.
    /// </summary>
    /// <param name="destination">The destination, as the route reads it.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("account/profile")]
    [InlineData("attacker.example")]
    [InlineData("?tab=security")]
    [InlineData("#keys")]
    [InlineData("javascript:alert(1)")]
    public void API_REDIR_001_AC1_ADestinationThatIsNotOnePathIsReplacedWithTheRoot(string? destination) =>
        Assert.Equal("/", ReturnDestination.Followed(destination));

    /// <summary>
    /// API-REDIR-001 AC2: an absolute or scheme-relative address, and a destination
    /// carrying a backslash, a tab, a line break, any other control character, a space
    /// or a character outside ASCII, wherever it stands, is not followed.
    /// </summary>
    /// <param name="destination">The destination, as the route reads it.</param>
    [Theory]
    [InlineData("https://attacker.example/collect")]
    [InlineData("//attacker.example")]
    [InlineData("/\\attacker.example")]
    [InlineData("\\/attacker.example")]
    [InlineData("/account\\profile")]
    [InlineData("/\t/attacker.example")]
    [InlineData("/\n/attacker.example")]
    [InlineData("/\r/attacker.example")]
    [InlineData("/account\t")]
    [InlineData("/account\r\n")]
    [InlineData("/\0/attacker.example")]
    [InlineData("/\u001B/attacker.example")]
    [InlineData("/\u007F/attacker.example")]
    [InlineData("/\u0085/attacker.example")]
    [InlineData("/ /attacker.example")]
    [InlineData("/account profile")]
    [InlineData(" /account")]
    [InlineData("/caf\u00E9")]
    [InlineData("/\u2028/attacker.example")]
    [InlineData("/\uFF0Fattacker.example")]
    public void API_REDIR_001_AC2_ADestinationOffThisApplicationOrNotPrintableIsNotFollowed(string destination) =>
        Assert.Equal("/", ReturnDestination.Followed(destination));
}
