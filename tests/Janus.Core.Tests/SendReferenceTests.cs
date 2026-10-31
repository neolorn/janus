using System.Security.Cryptography;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The reference one send carries to the host and back, which the host's report of an
/// outcome names (INT-SMS-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class SendReferenceTests
{
    /// <summary>
    /// INT-SMS-005: a drawn reference, as the host was handed it, reads back as itself.
    /// </summary>
    [Fact]
    public void INT_SMS_005_ADrawnReferenceReadsBackAsItself()
    {
        using var randomness = RandomNumberGenerator.Create();
        var drawn = SendReference.Draw(randomness);

        Assert.True(SendReference.TryParse(drawn.Value, out SendReference read));
        Assert.Equal(drawn, read);
    }

    /// <summary>
    /// INT-SMS-005 AC3: text that is no reference reads as none and is never a fault, so
    /// a host's report naming it changes nothing: nothing, text of another length, text
    /// outside the URL-safe alphabet, and padding.
    /// </summary>
    /// <param name="text">Text that is no reference.</param>
    [Theory]
    [InlineData("")]
    [InlineData("not-a-reference")]
    [InlineData("AAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAA+A")]
    [InlineData("AAAAAAAAAAAAAAAAAAAA/A")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")]
    [InlineData("AAAAAAAAAAAAAAAAAAAA A")]
    public void INT_SMS_005_AC3_TextThatIsNoReferenceReadsAsNone(string text)
    {
        Assert.False(SendReference.TryParse(text, out _));
        Assert.False(SendReference.TryParse(null, out _));
    }
}
