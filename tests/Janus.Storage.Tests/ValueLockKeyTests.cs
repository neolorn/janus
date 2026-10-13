using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Janus.Core;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The key of the lock a decision on a value is made under (CONV-DESIGN-003,
/// OPS-SEC-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class ValueLockKeyTests
{
    private static readonly byte[] Counted = [.. Enumerable.Range(0, 32).Select(index => (byte)index)];

    private static readonly FingerprintKeys Rotating = new(
        1,
        new Dictionary<int, ReadOnlyMemory<byte>>
        {
            [1] = Encoding.UTF8.GetBytes("the fingerprint key before the rotation"),
            [2] = Encoding.UTF8.GetBytes("the fingerprint key after the rotation"),
        });

    /// <summary>
    /// CONV-DESIGN-003: the key is the first eight bytes, read as a signed big-endian
    /// integer, of SHA-256 over the lock's name, one zero byte and the fingerprint.
    /// </summary>
    /// <param name="name">The lock's name.</param>
    /// <param name="expected">The key the rule gives for the fingerprint 00 to 1f.</param>
    [Theory]
    [InlineData(ValueLock.Identifier, unchecked((long)0xCF96642AD552C25D))]
    [InlineData(ValueLock.Username, unchecked((long)0x83C6DB703810BCB2))]
    public void CONV_DESIGN_003_TheKeyIsTheLeadingBytesOfTheDigestOfTheNameAndTheFingerprint(
        string name,
        long expected)
    {
        long key = ValueLock.Key(name, Counted);

        Assert.Equal(expected, key);
    }

    /// <summary>
    /// CONV-DESIGN-003: an email address and a phone number are locked under one name
    /// and a username under another.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_003_AUsernameIsLockedUnderItsOwnName()
    {
        string email = ValueLock.NameOf(IdentifierKind.Email);
        string phone = ValueLock.NameOf(IdentifierKind.Phone);
        string username = ValueLock.NameOf(IdentifierKind.Username);

        Assert.Equal(ValueLock.Identifier, email);
        Assert.Equal(ValueLock.Identifier, phone);
        Assert.Equal(ValueLock.Username, username);
    }

    /// <summary>
    /// CONV-DESIGN-003, OPS-SEC-003: a value is locked once for each fingerprint key
    /// version the process holds, each key computed from the fingerprint under that
    /// version.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_003_AValueIsLockedUnderEveryFingerprintKeyVersionHeld()
    {
        byte[] canonical = Encoding.UTF8.GetBytes("ahmed@example.com");

        IReadOnlyList<long> keys = ValueLock.Keys(ValueLock.Identifier, canonical, Rotating);

        long[] expected =
        [
            ValueLock.Key(ValueLock.Identifier, Fingerprint.Compute(canonical, Rotating.Versions[1].Span)),
            ValueLock.Key(ValueLock.Identifier, Fingerprint.Compute(canonical, Rotating.Versions[2].Span)),
        ];

        Assert.Equal(expected.Order(), keys.Order());
        Assert.NotEqual(expected[0], expected[1]);
    }

    /// <summary>
    /// CONV-DESIGN-003: the lock is the transaction's, so one asked for outside a
    /// transaction is a defect and not a lock quietly held to the connection's end.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task TakeAsync_OutsideATransaction_ThrowsAsync()
    {
        await using StoreContext context = DatabaseFixture.Context("Host=localhost");

        async Task TakenAsync() =>
            await ValueLock.TakeAsync(context, [1], TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(TakenAsync);
    }
}
