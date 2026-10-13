using System.Collections.Generic;
using System.Linq;
using Janus.Core.Unicode;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The Punycode a domain's ASCII form is written in, against the sample strings of
/// RFC 3492 section 7.1. Covers REG-DOM-001.
/// </summary>
[Trait("kind", "unit")]
public sealed class PunycodeTests
{
    /// <summary>
    /// The samples, each the code points of a label and its encoding.
    /// </summary>
    public static TheoryData<int[], string> Samples { get; } = new()
    {
        {
            [
                0x0644, 0x064A, 0x0647, 0x0645, 0x0627, 0x0628, 0x062A, 0x0643, 0x0644, 0x0645, 0x0648, 0x0634,
                0x0639, 0x0631, 0x0628, 0x064A, 0x061F,
            ],
            "egbpdaj6bu4bxfgehfvwxn"
        },
        {
            [0x4ED6, 0x4EEC, 0x4E3A, 0x4EC0, 0x4E48, 0x4E0D, 0x8BF4, 0x4E2D, 0x6587],
            "ihqwcrb4cv8a8dqg056pqjye"
        },
        {
            [0x0033, 0x5E74, 0x0042, 0x7D44, 0x91D1, 0x516B, 0x5148, 0x751F],
            "3B-ww4c5e180e575a65lsy2b"
        },
        {
            [
                0x004D, 0x0061, 0x006A, 0x0069, 0x3067, 0x004B, 0x006F, 0x0069, 0x3059, 0x308B, 0x0035, 0x79D2,
                0x524D,
            ],
            "MajiKoi5-783gue6qz075azm5e"
        },
        {
            [0x002D, 0x003E, 0x0020, 0x0024, 0x0031, 0x002E, 0x0030, 0x0030, 0x0020, 0x003C, 0x002D],
            "-> $1.00 <--"
        },
    };

    /// <summary>
    /// A label encodes to the string the standard gives for it.
    /// </summary>
    /// <param name="label">The label's code points.</param>
    /// <param name="expected">Its encoding.</param>
    [Theory]
    [MemberData(nameof(Samples))]
    public void TryEncode_ASampleOfTheStandard_GivesItsEncoding(int[] label, string expected)
    {
        bool encoded = Punycode.TryEncode([.. label], out string text);

        Assert.True(encoded);
        Assert.Equal(expected, text);
    }

    /// <summary>
    /// An encoding decodes to the label the standard gives for it.
    /// </summary>
    /// <param name="expected">The label's code points.</param>
    /// <param name="text">Its encoding.</param>
    [Theory]
    [MemberData(nameof(Samples))]
    public void TryDecode_ASampleOfTheStandard_GivesItsLabel(int[] expected, string text)
    {
        bool decoded = Punycode.TryDecode([.. text.Select(character => (int)character)], out List<int> label);

        Assert.True(decoded);
        Assert.Equal(expected, label);
    }

    /// <summary>
    /// What is no encoding is refused: a character that is no digit of the encoding, an
    /// integer that runs past the end, and one that names a code point past the last.
    /// </summary>
    /// <param name="text">The text.</param>
    [Theory]
    [InlineData("a b")]
    [InlineData("abc-!")]
    [InlineData("b")]
    [InlineData("a-99999999")]
    public void TryDecode_WhatIsNoEncoding_IsRefused(string text)
    {
        bool decoded = Punycode.TryDecode([.. text.Select(character => (int)character)], out _);

        Assert.False(decoded);
    }
}
