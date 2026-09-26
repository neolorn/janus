using System;
using System.Collections.Generic;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The fingerprint key set a host supplies: what it refuses and what it hands back
/// (PRIV-RIGHT-005c, OPS-SEC-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class FingerprintKeysTests
{
    private static readonly ReadOnlyMemory<byte> Material = new byte[32];

    /// <summary>
    /// OPS-SEC-003: the version a fingerprint is written under is the current one.
    /// </summary>
    [Fact]
    public void Current_ASetHoldingSeveralVersions_IsTheCurrentVersionsKey()
    {
        byte[] current = new byte[32];
        current[0] = 2;

        var keys = new FingerprintKeys(2, new Dictionary<int, ReadOnlyMemory<byte>>
        {
            [1] = Material,
            [2] = current,
        });

        Assert.True(keys.Current.Span.SequenceEqual(current));
    }

    /// <summary>
    /// PRIV-RIGHT-005c: a set that cannot compute a fingerprint is refused where it is
    /// supplied, not where an identifier is first looked up.
    /// </summary>
    [Fact]
    public void Construction_WithoutTheCurrentVersion_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new FingerprintKeys(2, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = Material }));

    /// <summary>
    /// CONV-CODE-006: a set that was never supplied is refused.
    /// </summary>
    [Fact]
    public void Construction_WithoutASet_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new FingerprintKeys(1, null!));
}
