using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The device description a credential or a session records (AUTH-FACT-001,
/// AUTH-SESS-013).
/// </summary>
[Trait("kind", "unit")]
public sealed class DeviceDescriptionTests
{
    /// <summary>
    /// AUTH-FACT-001: the default credential label is the two parts joined by a
    /// space.
    /// </summary>
    [Fact]
    public void AUTH_FACT_001_TheDefaultLabelIsTheTwoPartsJoined() =>
        Assert.Equal("Firefox Fedora", new DeviceDescription("Firefox", "Fedora").ToString());

    /// <summary>
    /// AUTH-FACT-001: each part is at most sixty-four characters, whichever part it
    /// is.
    /// </summary>
    /// <param name="browser">The browser.</param>
    /// <param name="os">The operating system.</param>
    [Theory]
    [InlineData("Firefox", "")]
    [InlineData("", "Fedora")]
    [InlineData("Firefox", "  ")]
    public void AUTH_FACT_001_APartThatSaysNothingIsRefused(string browser, string os) =>
        Assert.Throws<ArgumentException>(() => new DeviceDescription(browser, os));

    /// <summary>
    /// AUTH-FACT-001: a part longer than sixty-four characters is refused rather than
    /// truncated, the header it comes from being under nobody's control.
    /// </summary>
    [Fact]
    public void AUTH_FACT_001_APartLongerThanTheLimitIsRefused()
    {
        string longest = new('a', DeviceDescription.MaximumLength);

        Assert.Equal(longest, new DeviceDescription(longest, "Fedora").Browser);
        Assert.Throws<ArgumentException>(() => new DeviceDescription(longest + "a", "Fedora"));
        Assert.Throws<ArgumentException>(() => new DeviceDescription("Firefox", longest + "a"));
    }

    /// <summary>
    /// AUTH-FACT-001: the limit holds on a description changed after it was made, a
    /// record's <c>with</c> expression being another way in.
    /// </summary>
    [Fact]
    public void AUTH_FACT_001_APartChangedAfterwardsIsHeldToTheSameLimit()
    {
        var described = new DeviceDescription("Firefox", "Fedora");

        Assert.Throws<ArgumentException>(() =>
            described with { Browser = new string('a', DeviceDescription.MaximumLength + 1) });
    }
}
