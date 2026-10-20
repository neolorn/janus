using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Janus.Core;
using Janus.Storage.Privacy.SubjectKeys;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// A subject's data key as it is lent to a host's use: what it does while lent, and
/// what is left of it once returned (PRIV-RIGHT-005a, CONV-CODE-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class LentFieldsTests
{
    private static readonly SubjectId Recipient = new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly ResourceType ParcelType = ResourceType.Parse("parcel");

    private static readonly HashSet<(ResourceType Type, string Field)> Declared = [(ParcelType, "RecipientName")];

    /// <summary>
    /// PRIV-RIGHT-005a AC20: the data key is cleared when the loan ends, and nothing is
    /// done under it afterwards.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC20_TheKeyIsClearedWhenTheLoanEnds()
    {
        byte[] dataKey = RandomNumberGenerator.GetBytes(32);
        using var randomness = RandomNumberGenerator.Create();
        var lent = new LentFields(Recipient, dataKey, Declared, randomness);

        ReadOnlyMemory<byte> stored = lent.Encrypt(ParcelType, "RecipientName", "Ahmed Mahmoud")
            .Match(value => value, failure => throw new InvalidOperationException(failure.Code.ToString()));
        lent.Return();

        Assert.All(dataKey, part => Assert.Equal(0, part));
        Assert.Throws<ObjectDisposedException>(() => lent.Decrypt(ParcelType, "RecipientName", stored));
    }
}
