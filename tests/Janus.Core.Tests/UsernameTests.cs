using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What a username may be: the profile of RFC 8265, narrowed to letters and digits and
/// held to one script per word (REG-IDENT-001, REG-IDENT-009, IDN-ACCT-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class UsernameTests
{
    /// <summary>
    /// REG-IDENT-001: the profile lowercases and composes, so one username is one
    /// username however it was typed.
    /// </summary>
    /// <param name="entered">The username as it was entered.</param>
    /// <param name="expected">The username in the profile's form.</param>
    [Theory]
    [InlineData("ahmed", "ahmed")]
    [InlineData("Ahmed", "ahmed")]
    [InlineData("AHMED", "ahmed")]
    [InlineData("\uFF41hmed", "ahmed")]
    [InlineData("a\u0301hmed", "\u00E1hmed")]
    [InlineData("\u0645\u062D\u0645\u062F", "\u0645\u062D\u0645\u062F")]
    [InlineData("ahmed2", "ahmed2")]
    public void REG_IDENT_001_AUsernameTakesTheProfilesForm(string entered, string expected)
    {
        Assert.True(Username.TryParse(entered, out Username username));
        Assert.Equal(expected, username.Value);
    }

    /// <summary>
    /// REG-IDENT-001: letters and digits, and nothing else. A space, a punctuation mark
    /// and a symbol are each refused, though the profile itself would admit the last
    /// two.
    /// </summary>
    /// <param name="entered">The value as it was entered.</param>
    [Theory]
    [InlineData("ahmed ismail")]
    [InlineData("ahmed.ismail")]
    [InlineData("ahmed_ismail")]
    [InlineData("ahmed-ismail")]
    [InlineData("ahmed@example.com")]
    [InlineData("ahmed!")]
    public void REG_IDENT_001_NothingButLettersAndDigitsIsAUsername(string entered) =>
        Assert.False(Username.TryParse(entered, out _));

    /// <summary>
    /// REG-IDENT-001: a username is 3 to 32 characters, counted in Unicode scalar
    /// values rather than in the units a particular encoding happens to use.
    /// </summary>
    /// <param name="entered">The value as it was entered.</param>
    /// <param name="accepted">Whether it is within the limits.</param>
    [Theory]
    [InlineData("ah", false)]
    [InlineData("ahm", true)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    public void REG_IDENT_001_AUsernameIsThreeToThirtyTwoCharacters(string entered, bool accepted) =>
        Assert.Equal(accepted, Username.TryParse(entered, out _));

    /// <summary>
    /// IDN-ACCT-005 AC1: a username mixing scripts inside its one word is refused, and
    /// the same username wholly in either script is accepted.
    /// </summary>
    [Fact]
    public void IDN_ACCT_005_AC1_AUsernameMixingScriptsIsRefused()
    {
        Assert.False(Username.TryParse("\u0430hmed", out _));
        Assert.True(Username.TryParse("\u0430\u0445\u043C\u0435\u0434", out _));
        Assert.True(Username.TryParse("ahmed", out _));
    }

    /// <summary>
    /// REG-IDENT-001: a username carrying right-to-left letters satisfies the Bidi Rule
    /// the profile applies, so a mixed-direction username is refused before it is
    /// stored.
    /// </summary>
    [Fact]
    public void REG_IDENT_001_AUsernameSatisfiesTheBidiRule()
    {
        Assert.True(Username.TryParse("\u0645\u062D\u0645\u062F\u0662", out _));
        Assert.False(Username.TryParse("\u0645\u062D\u0645\u062Fa", out _));
    }

    /// <summary>
    /// An unset value reads as an empty string rather than throwing.
    /// </summary>
    [Fact]
    public void Value_UnsetUsername_IsEmpty()
    {
        Assert.Equal(string.Empty, default(Username).Value);
        Assert.Equal(string.Empty, default(Username).ToString());
    }

    /// <summary>
    /// An absent value is a programming fault, not a malformed username.
    /// </summary>
    [Fact]
    public void TryParse_AbsentValue_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Username.TryParse(null!, out _));
}
