using System;
using System.Collections.Generic;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The key-encryption key set a host supplies: what it refuses and what it hands back
/// (PRIV-RIGHT-005a, OPS-SEC-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class KeyEncryptionKeysTests
{
    private static readonly ReadOnlyMemory<byte> Material = new byte[32];

    /// <summary>
    /// OPS-SEC-003: the version a new subject key is wrapped under is the current one.
    /// </summary>
    [Fact]
    public void Current_ASetHoldingSeveralVersions_IsTheCurrentVersionsKey()
    {
        var earlier = new ReadOnlyMemory<byte>(new byte[32]);

        var keys = new KeyEncryptionKeys(2, new Dictionary<int, ReadOnlyMemory<byte>>
        {
            [1] = earlier,
            [2] = Material,
        });

        Assert.True(keys.Current.Span.SequenceEqual(Material.Span));
    }

    /// <summary>
    /// PRIV-RIGHT-005a: a set that cannot wrap is refused where it is supplied, not
    /// where a subject key is first written.
    /// </summary>
    [Fact]
    public void Construction_WithoutTheCurrentVersion_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new KeyEncryptionKeys(2, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = Material }));

    /// <summary>
    /// PRIV-RIGHT-005a: key material of a length no AES key has is refused at startup.
    /// </summary>
    [Fact]
    public void Construction_WithMaterialOfNoAesKeyLength_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>>
            {
                [1] = new byte[20],
            }));

    /// <summary>
    /// CONV-CODE-006: a set that was never supplied is refused.
    /// </summary>
    [Fact]
    public void Construction_WithoutASet_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new KeyEncryptionKeys(1, null!));
}
