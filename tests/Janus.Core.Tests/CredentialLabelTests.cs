using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What a person may call one of their enrolled credentials (AUTH-FACT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class CredentialLabelTests
{
    /// <summary>
    /// AUTH-FACT-001: a label of one to sixty-four characters is accepted and one
    /// outside that is not.
    /// </summary>
    /// <param name="entered">The label as it was entered.</param>
    /// <param name="accepted">Whether it is a label this library accepts.</param>
    [Theory]
    [InlineData("a", true)]
    [InlineData("Work laptop", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void AUTH_FACT_001_ALabelIsOneToSixtyFourCharacters(string entered, bool accepted) =>
        Assert.Equal(accepted, CredentialLabel.TryParse(entered, out _));

    /// <summary>
    /// AUTH-FACT-001: the bound is sixty-four characters, and a label of exactly that
    /// is accepted.
    /// </summary>
    [Fact]
    public void AUTH_FACT_001_TheBoundIsSixtyFourCharacters()
    {
        Assert.True(CredentialLabel.TryParse(new string('a', CredentialLabel.MaximumLength), out _));
        Assert.False(
            CredentialLabel.TryParse(new string('a', CredentialLabel.MaximumLength + 1), out _));
    }

    /// <summary>
    /// AUTH-FACT-001 AC6: an enrolment that supplies no label takes the client's
    /// description of the device.
    /// </summary>
    [Fact]
    public void AUTH_FACT_001_AC6_TheDefaultLabelIsTheDeviceDescription() =>
        Assert.Equal(
            "Firefox Fedora",
            CredentialLabel.Of(new DeviceDescription("Firefox", "Fedora")).Value);

    /// <summary>
    /// AUTH-FACT-001: a label is shown back as the person wrote it, with the spaces
    /// and the normalization form settled by the profile.
    /// </summary>
    [Fact]
    public void AUTH_FACT_001_ALabelIsShownBackAsItWasWritten()
    {
        Assert.True(CredentialLabel.TryParse("  Work   Laptop  ", out CredentialLabel label));
        Assert.Equal("Work Laptop", label.Value);
        Assert.Equal("Work Laptop", label.ToString());
    }

    /// <summary>
    /// A label that was never read is empty rather than absent, so no caller holds a
    /// null string.
    /// </summary>
    [Fact]
    public void Value_ALabelNeverRead_IsEmpty() => Assert.Equal(string.Empty, default(CredentialLabel).Value);

    /// <summary>
    /// A value that is not a label is refused rather than trimmed to one.
    /// </summary>
    [Fact]
    public void Of_ADescriptionThatDoesNotReadAsALabel_IsRefused() =>
        Assert.Throws<ArgumentException>(() =>
            CredentialLabel.Of(new DeviceDescription(
                new string('a', DeviceDescription.MaximumLength),
                new string('b', DeviceDescription.MaximumLength))));
}
