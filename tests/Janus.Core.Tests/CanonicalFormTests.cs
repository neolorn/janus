using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The canonical form of an identifier and the digit mapping a phone number takes
/// instead (IDN-ACCT-004, IDN-ACCT-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class CanonicalFormTests
{
    private const string Composed = "\u00E1hmed@example.com";
    private const string Decomposed = "a\u0301hmed@example.com";
    private const string Fullwidth = "\uFF41hmed@example.com";
    private const string Ascii = "ahmed@example.com";

    /// <summary>
    /// IDN-ACCT-004 AC1: an address entered in decomposed form and the same address
    /// entered in composed form are one value to the database, so the second
    /// registration finds the first account rather than making another.
    /// </summary>
    [Fact]
    public void IDN_ACCT_004_AC1_CompositionDifferencesShareOneCanonicalForm() =>
        Assert.Equal(CanonicalForm.Of(Composed), CanonicalForm.Of(Decomposed));

    /// <summary>
    /// IDN-ACCT-004 AC4: the fullwidth and the ASCII spelling of one address are one
    /// account.
    /// </summary>
    [Fact]
    public void IDN_ACCT_004_AC4_FullwidthAndAsciiAddressesShareOneCanonicalForm()
    {
        Assert.Equal(Ascii, CanonicalForm.Of(Fullwidth));
        Assert.Equal(CanonicalForm.Of(Ascii), CanonicalForm.Of(Fullwidth));
    }

    /// <summary>
    /// IDN-ACCT-004 AC5: a number entered in Arabic-Indic or extended Arabic-Indic
    /// digits stores as the E.164 value its ASCII-digit form stores as.
    /// </summary>
    /// <param name="entered">The number as it was entered.</param>
    [Theory]
    [InlineData("+\u0662\u0660\u0661\u0660\u0660\u0661\u0662\u0663\u0664\u0665\u0666\u0667")]
    [InlineData("+\u06F2\u06F0\u06F1\u06F0\u06F0\u06F1\u06F2\u06F3\u06F4\u06F5\u06F6\u06F7")]
    [InlineData("+201001234567")]
    public void IDN_ACCT_004_AC5_DigitsOfAnyScriptStoreAsTheSameNumber(string entered) =>
        Assert.Equal("+201001234567", CanonicalForm.AsciiDigits(entered));

    /// <summary>
    /// IDN-ACCT-004 AC5: the digit mapping touches digits and nothing else, so a
    /// separator or an extension letter survives it for the E.164 parser to reject.
    /// </summary>
    [Fact]
    public void IDN_ACCT_004_AC5_NothingButADigitIsMapped() =>
        Assert.Equal("+20 (100) x-123", CanonicalForm.AsciiDigits("+\u0662\u0660 (\u0661\u0660\u0660) x-123"));

    /// <summary>
    /// IDN-ACCT-004 AC6: the version the tables were generated from is the version the
    /// library reports, so a fingerprint can record the form it was derived under.
    /// </summary>
    [Fact]
    public void IDN_ACCT_004_AC6_ThePinnedUnicodeVersionIsReported() =>
        Assert.Equal("17.0.0", CanonicalForm.UnicodeVersion);

    /// <summary>
    /// A default-ignorable code point is not part of the identifier, so an address
    /// carrying one is the same account as the address without it.
    /// </summary>
    [Fact]
    public void IDN_ACCT_004_AC1_DefaultIgnorableCodePointsAreRemoved() =>
        Assert.Equal(Ascii, CanonicalForm.Of("ah\u00ADmed@example.com"));

    /// <summary>
    /// IDN-ACCT-006 AC1 and AC2: capitals make no difference to which account an
    /// address names, whichever of them the person typed.
    /// </summary>
    /// <param name="entered">The address as it was entered.</param>
    [Theory]
    [InlineData("Ahmed@example.com")]
    [InlineData("AHMED@EXAMPLE.COM")]
    [InlineData("aHmEd@ExAmPlE.cOm")]
    public void IDN_ACCT_006_AC1_CapitalsDoNotChangeTheCanonicalForm(string entered) =>
        Assert.Equal(Ascii, CanonicalForm.Of(entered));

    /// <summary>
    /// Case folding is full folding, not the simple mapping: the sharp s of a German
    /// address folds to two letters rather than staying one.
    /// </summary>
    [Fact]
    public void IDN_ACCT_006_AC1_FoldingIsFullFolding()
    {
        Assert.Equal("fussball", CanonicalForm.Of("Fu\u00DFball"));
        Assert.Equal("fussball", CanonicalForm.Of("FU\u1E9EBALL"));
    }

    /// <summary>
    /// Compatibility equivalents fold to the letters they render as, so a Roman numeral
    /// is not a second spelling of an account.
    /// </summary>
    [Fact]
    public void IDN_ACCT_004_AC1_CompatibilityEquivalentsFoldToTheirLetters() =>
        Assert.Equal("richard iv", CanonicalForm.Of("Richard \u2163"));

    /// <summary>
    /// An absent value is a programming fault, not an empty identifier.
    /// </summary>
    [Fact]
    public void Of_AbsentValue_Throws() =>
        Assert.Throws<ArgumentNullException>(() => CanonicalForm.Of(null!));

    /// <summary>
    /// An absent value is a programming fault, not an empty number.
    /// </summary>
    [Fact]
    public void AsciiDigits_AbsentValue_Throws() =>
        Assert.Throws<ArgumentNullException>(() => CanonicalForm.AsciiDigits(null!));
}
