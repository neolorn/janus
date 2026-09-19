using System;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Xunit;

namespace Janus.Privacy.Tests.SubjectKeys;

/// <summary>
/// The subject key row: what it admits, what a rotation does to it and what erasure
/// leaves behind (PRIV-RIGHT-005a, IDN-PRIN-003, OPS-SEC-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class SubjectKeyTests
{
    private static readonly SubjectId Ahmed = new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    /// <summary>
    /// PRIV-RIGHT-005a: a newly recorded key names the scheme it is written under and
    /// the key-encryption key version it is wrapped by.
    /// </summary>
    [Fact]
    public void Wrapped_ANewKey_CarriesTheSchemeAndTheVersion()
    {
        var key = SubjectKey.Wrapped(Ahmed, 1, new byte[40]);

        Assert.Equal(PersonalDataFormat.Marker, key.FormatMarker);
        Assert.Equal(1, key.KeyVersion);
        Assert.False(key.IsErased);
    }

    /// <summary>
    /// PRIV-RIGHT-005a: a value that is not the length the scheme produces never
    /// reaches a row.
    /// </summary>
    [Fact]
    public void Wrapped_AValueOfAnotherLength_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SubjectKey.Wrapped(Ahmed, 1, new byte[32]));

    /// <summary>
    /// PRIV-RIGHT-005a AC9: erasure overwrites the wrapped key with 32 zero bytes under
    /// the erased marker, and the row stays.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC9_ErasureOverwritesTheWrappedKey()
    {
        var key = SubjectKey.Wrapped(Ahmed, 1, new byte[40]);

        key.Erase();

        Assert.True(key.IsErased);
        Assert.Equal(PersonalDataFormat.ErasedMarker, key.FormatMarker);
        Assert.Equal(new byte[32], key.WrappedKey.ToArray());
        Assert.Equal(Ahmed, key.Subject);
    }

    /// <summary>
    /// OPS-SEC-003 AC2: a rotation moves the key to the new version and leaves the
    /// subject alone.
    /// </summary>
    [Fact]
    public void ReWrap_ALaterVersion_MovesTheKeyToIt()
    {
        var key = SubjectKey.Wrapped(Ahmed, 1, new byte[40]);
        byte[] reWrapped = [.. new byte[39], 0x01];

        key.ReWrap(2, reWrapped);

        Assert.Equal(2, key.KeyVersion);
        Assert.Equal(reWrapped, key.WrappedKey.ToArray());
    }

    /// <summary>
    /// OPS-SEC-003 AC2: no subject key is re-wrapped twice under one version, so a
    /// resumed run cannot move a key backwards or sideways.
    /// </summary>
    [Fact]
    public void ReWrap_TheVersionAlreadyHeld_Throws()
    {
        var key = SubjectKey.Wrapped(Ahmed, 2, new byte[40]);

        Assert.Throws<ArgumentOutOfRangeException>(() => key.ReWrap(2, new byte[40]));
    }

    /// <summary>
    /// PRIV-RIGHT-005a: an erased key is never brought back by a rotation.
    /// </summary>
    [Fact]
    public void ReWrap_AnErasedKey_Throws()
    {
        var key = SubjectKey.Wrapped(Ahmed, 1, new byte[40]);
        key.Erase();

        Assert.Throws<InvalidOperationException>(() => key.ReWrap(2, new byte[40]));
    }
}
