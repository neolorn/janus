using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The opaque subject identifier: what it is derived from and what shape it carries
/// (IDN-ACCT-002, CONV-DESIGN-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class SubjectIdTests
{
    private const byte Drawn = 0x11;

    /// <summary>
    /// IDN-ACCT-002 AC1: the identifier is the drawn bytes with the two fields RFC 9562
    /// section 5.4 fixes, so no part of it can have come from an attribute of the
    /// person.
    /// </summary>
    [Fact]
    public void IDN_ACCT_002_AC1_TheIdentifierIsTheDrawnBytesAndNothingElse()
    {
        byte[] expected = [.. Enumerable.Repeat(Drawn, 16)];
        expected[6] = 0x41;
        expected[8] = 0x91;

        using var randomness = new FixedRandomness(Drawn);
        var subject = SubjectId.New(randomness);

        Assert.Equal(expected, subject.Value.ToByteArray(bigEndian: true));
    }

    /// <summary>
    /// CONV-DESIGN-004: the subject identifier is a version 4 value, so it carries no
    /// creation instant.
    /// </summary>
    [Fact]
    public void New_AnyRandomness_ProducesAVersionFourValue()
    {
        using var randomness = RandomNumberGenerator.Create();

        var subject = SubjectId.New(randomness);

        Assert.Equal(4, subject.Value.Version);
        Assert.Equal(0x80, subject.Value.ToByteArray(bigEndian: true)[8] & 0xC0);
    }

    /// <summary>
    /// IDN-ACCT-002: two accounts never share an identifier.
    /// </summary>
    [Fact]
    public void New_TwoCalls_ProduceDifferentIdentifiers()
    {
        using var randomness = RandomNumberGenerator.Create();

        Assert.NotEqual(SubjectId.New(randomness), SubjectId.New(randomness));
    }

    /// <summary>
    /// CONV-CODE-006: the generator refuses a source it was not given.
    /// </summary>
    [Fact]
    public void New_NoRandomness_Throws() =>
        Assert.Throws<ArgumentNullException>(() => SubjectId.New(null!));

    /// <summary>
    /// PRIV-RIGHT-005a AC18 and CONV-DESIGN-004 (D-174): the max UUID of RFC 9562 is the
    /// deployment's data key's row, so no subject can be made from it.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC18_NoSubjectIsMadeFromTheMaxUuid() =>
        Assert.Throws<ArgumentException>(() => new SubjectId(Guid.AllBitsSet));

    /// <summary>
    /// CONV-DESIGN-004 AC2: a subject read as a route carries it is the identifier the
    /// text names, and the max UUID or text that names no identifier is not read at all.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_004_AC2_ASubjectIsReadFromTextAndTheMaxUuidIsNot()
    {
        var issued = Guid.Parse("4a1d1c8e-6b6f-4c3e-9f0a-2d5e7b8c9d01");

        Assert.True(SubjectId.TryParse(issued.ToString(), provider: null, out SubjectId read));
        Assert.Equal(issued, read.Value);
        Assert.False(SubjectId.TryParse(Guid.AllBitsSet.ToString(), provider: null, out _));
        Assert.False(SubjectId.TryParse("not-a-subject", provider: null, out _));
        Assert.Throws<FormatException>(() => SubjectId.Parse(Guid.AllBitsSet.ToString(), provider: null));
    }

    /// <summary>
    /// PRIV-RIGHT-005a (D-174): the nil subject stays a value, since it means no subject
    /// wherever a column admits it.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC18_TheNilSubjectIsStillMade() =>
        Assert.Equal(Guid.Empty, new SubjectId(Guid.Empty).Value);

    /// <summary>
    /// PRIV-RIGHT-005a AC18 (D-174): a subject written as JSON reads back through the
    /// constructor, so a stored one keeps its value and the max UUID is refused on read.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC18_AWrittenSubjectReadsBackThroughTheRefusal()
    {
        using var randomness = RandomNumberGenerator.Create();
        var subject = SubjectId.New(randomness);

        Assert.Equal(subject, JsonSerializer.Deserialize<SubjectId>(JsonSerializer.Serialize(subject)));
        Assert.Throws<ArgumentException>(() =>
            JsonSerializer.Deserialize<SubjectId>("""{"Value":"ffffffff-ffff-ffff-ffff-ffffffffffff"}"""));
    }
}
