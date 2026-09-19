using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What a legal name may be: Normalization Form C, bounded in scalar values and held to
/// one script per word (REG-PROF-001, IDN-ATTR-007, IDN-ACCT-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class LegalNameTests
{
    /// <summary>
    /// REG-PROF-001: a legal name is 1 to 200 Unicode scalar values, counted after the
    /// normalization rather than before it.
    /// </summary>
    /// <param name="length">How many characters the name was entered with.</param>
    /// <param name="accepted">Whether it is within the bound.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void REG_PROF_001_ALegalNameIsOneToTwoHundredScalarValues(int length, bool accepted) =>
        Assert.Equal(accepted, LegalName.TryParse(new string('a', length), out _));

    /// <summary>
    /// REG-PROF-001: the bound is counted after the composition, so a name written in
    /// decomposed form is the length its composed form is.
    /// </summary>
    [Fact]
    public void REG_PROF_001_TheBoundIsCountedAfterTheNormalization()
    {
        string decomposed = string.Concat(System.Linq.Enumerable.Repeat("á", 200));

        Assert.True(LegalName.TryParse(decomposed, out LegalName name));
        Assert.Equal(new string('á', 200), name.Value);
    }

    /// <summary>
    /// REG-PROF-001: the stored form is Normalization Form C, so one name written in
    /// two compositions is one name.
    /// </summary>
    /// <param name="entered">The name as it was entered.</param>
    /// <param name="expected">The name in Normalization Form C.</param>
    [Theory]
    [InlineData("Ahmed Ismail", "Ahmed Ismail")]
    [InlineData("Áhmed", "Áhmed")]
    [InlineData("أحمد", "أحمد")]
    public void REG_PROF_001_ALegalNameIsHeldInFormC(string entered, string expected)
    {
        Assert.True(LegalName.TryParse(entered, out LegalName name));
        Assert.Equal(expected, name.Value);
    }

    /// <summary>
    /// REG-PROF-001: unlike the display name, a legal name takes no compatibility
    /// mapping, so a fullwidth letter stays the letter the person entered.
    /// </summary>
    [Fact]
    public void REG_PROF_001_ALegalNameKeepsWhatCompatibilityNormalizationWouldFold()
    {
        Assert.True(LegalName.TryParse("Ａhmed", out LegalName name));
        Assert.Equal("Ａhmed", name.Value);
    }

    /// <summary>
    /// IDN-ACCT-005 AC1 and AC2: a word of two scripts is refused and words of
    /// different scripts beside each other are not.
    /// </summary>
    [Fact]
    public void IDN_ACCT_005_AC1_ALegalNameMixingScriptsWithinAWordIsRefused()
    {
        Assert.False(LegalName.TryParse("Аhmed", out _));
        Assert.True(LegalName.TryParse("أحمد Ahmed", out _));
    }

    /// <summary>
    /// An absent value is not an empty legal name; the caller decides whether the field
    /// was given at all.
    /// </summary>
    [Fact]
    public void TryParse_AnAbsentValue_Throws() =>
        Assert.Throws<System.ArgumentNullException>(() => LegalName.TryParse(null!, out _));
}
