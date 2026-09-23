using System;
using System.Collections.Generic;
using System.Text;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// What the library does when the two values the secrets manager holds are not there
/// to be had (AUTH-KEY-002, OPS-SEC-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class KeyMaterialTests
{
    private const string Connection = "Host=nowhere;Database=identity";

    /// <summary>
    /// AUTH-KEY-002 AC2: without the key-encryption key the library does not start,
    /// and what stops it names why.
    /// </summary>
    [Fact]
    public void AUTH_KEY_002_AC2_StartupFailsNamedWithoutTheKeyEncryptionKey() =>
        Assert.Equal(
            ErrorCodes.StartupKeyUnavailable,
            Refused(keys: null, fingerprintKey: new byte[32]));

    /// <summary>
    /// AUTH-KEY-002 AC2: the fingerprint key is read from the same place and the
    /// library starts without it no more than it starts without the other.
    /// </summary>
    [Fact]
    public void AUTH_KEY_002_AC2_StartupFailsNamedWithoutTheFingerprintKey() =>
        Assert.Equal(
            ErrorCodes.StartupKeyUnavailable,
            Refused(Usable, fingerprintKey: default));

    /// <summary>
    /// AUTH-KEY-002 AC2: a fingerprint key shorter than the hash it computes is no
    /// key, and is refused as one that is absent is.
    /// </summary>
    [Fact]
    public void AUTH_KEY_002_AC2_StartupFailsNamedOnAFingerprintKeyShorterThanTheHash() =>
        Assert.Equal(
            ErrorCodes.StartupKeyUnavailable,
            Refused(Usable, new byte[16]));

    private static KeyEncryptionKeys Usable =>
        new(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] });

    private static ErrorCode? Refused(KeyEncryptionKeys? keys, ReadOnlyMemory<byte> fingerprintKey) =>
        Assert.Throws<StartupException>(() => new ServiceCollection().AddJanus(
                Connection,
                keys!,
                fingerprintKey,
                Encoding.UTF8.GetBytes("the secret this application presents"),
                HostFixture.Declaration(),
                ApplicationKind.Public))
            .Failure?.Code;
}
