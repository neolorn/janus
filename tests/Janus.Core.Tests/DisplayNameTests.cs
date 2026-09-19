using System.Text;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What a display name may be: the Nickname profile of RFC 8266, bounded in bytes and
/// held to one script per word (REG-PROF-001, IDN-ATTR-007, IDN-ACCT-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class DisplayNameTests
{
    /// <summary>
    /// REG-PROF-001 AC1: 64 bytes is a display name and 65 is not, in whichever script
    /// the person writes, so a script that costs three bytes a character reaches the
    /// bound sooner in characters and at the same place in bytes.
    /// </summary>
    /// <param name="entered">The name as it was entered.</param>
    /// <param name="accepted">Whether it is within the bound.</param>
    [Theory]
    [InlineData("", false)]
    [InlineData("Ahmed", true)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", true)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", false)]
    public void REG_PROF_001_AC1_ADisplayNameIsAtMostSixtyFourBytes(string entered, bool accepted) =>
        Assert.Equal(accepted, DisplayName.TryParse(entered, out _));

    /// <summary>
    /// REG-PROF-001 AC1: the bound is counted in bytes, so a name of 21 Arabic
    /// characters is a display name and one of 33 is not, both being far from the
    /// 64-character mark.
    /// </summary>
    [Fact]
    public void REG_PROF_001_AC1_TheBoundIsCountedInBytesAndNotInCharacters()
    {
        string within = new('م', 32);
        string beyond = new('م', 33);

        Assert.Equal(DisplayName.MaximumBytes, Encoding.UTF8.GetByteCount(within));
        Assert.True(DisplayName.TryParse(within, out _));
        Assert.False(DisplayName.TryParse(beyond, out _));
    }

    /// <summary>
    /// REG-PROF-001: the profile settles the spaces and the normalization form and
    /// leaves the case alone, because a display name is shown back as it was written.
    /// </summary>
    /// <param name="entered">The name as it was entered.</param>
    /// <param name="expected">The name in the profile's form.</param>
    [Theory]
    [InlineData("Ahmed Ismail", "Ahmed Ismail")]
    [InlineData("  Ahmed Ismail  ", "Ahmed Ismail")]
    [InlineData("Ahmed Ismail", "Ahmed Ismail")]
    [InlineData("Ahmed   Ismail", "Ahmed Ismail")]
    [InlineData("Áhmed", "Áhmed")]
    public void REG_PROF_001_ADisplayNameTakesTheProfilesForm(string entered, string expected)
    {
        Assert.True(DisplayName.TryParse(entered, out DisplayName name));
        Assert.Equal(expected, name.Value);
    }

    /// <summary>
    /// IDN-ACCT-005 AC1 and AC2: a word of two scripts is refused and words of
    /// different scripts beside each other are not.
    /// </summary>
    [Fact]
    public void IDN_ACCT_005_AC1_ADisplayNameMixingScriptsWithinAWordIsRefused()
    {
        Assert.False(DisplayName.TryParse("Аhmed", out _));
        Assert.True(DisplayName.TryParse("أحمد Ahmed", out _));
    }

    /// <summary>
    /// An absent value is not an empty display name; the caller decides whether the
    /// field was given at all.
    /// </summary>
    [Fact]
    public void TryParse_AnAbsentValue_Throws() =>
        Assert.Throws<System.ArgumentNullException>(() => DisplayName.TryParse(null!, out _));
}
