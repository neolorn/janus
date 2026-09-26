using System;
using System.Collections.Generic;
using System.Text;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Sending;
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
            Refused(keys: null, Fingerprints(new byte[32])));

    /// <summary>
    /// AUTH-KEY-002 AC2: the fingerprint key is read from the same place and the
    /// library starts without it no more than it starts without the other.
    /// </summary>
    [Fact]
    public void AUTH_KEY_002_AC2_StartupFailsNamedWithoutTheFingerprintKey() =>
        Assert.Equal(
            ErrorCodes.StartupKeyUnavailable,
            Refused(Usable, fingerprintKeys: null));

    /// <summary>
    /// AUTH-KEY-002 AC2: a fingerprint key shorter than the hash it computes is no
    /// key, and is refused as one that is absent is.
    /// </summary>
    [Fact]
    public void AUTH_KEY_002_AC2_StartupFailsNamedOnAFingerprintKeyShorterThanTheHash() =>
        Assert.Equal(
            ErrorCodes.StartupKeyUnavailable,
            Refused(Usable, Fingerprints(new byte[16])));

    /// <summary>
    /// AUTH-KEY-002 AC2, OPS-SEC-003: a version retained beside the current one is read
    /// as the current one is, and is refused when it is short as the current one is.
    /// </summary>
    [Fact]
    public void AUTH_KEY_002_AC2_StartupFailsNamedOnARetainedFingerprintKeyShorterThanTheHash() =>
        Assert.Equal(
            ErrorCodes.StartupKeyUnavailable,
            Refused(
                Usable,
                new FingerprintKeys(2, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[16], [2] = new byte[32] })));

    /// <summary>
    /// OPS-MIG-003a, PRIV-RET-002: the maintenance credential is read from the same place,
    /// and the library does not start without it, since without it the audit trail stops
    /// taking rows once the months created ahead have passed.
    /// </summary>
    [Fact]
    public void OPS_MIG_003a_StartupFailsNamedWithoutTheMaintenanceCredential()
    {
        Error? refused = Assert.Throws<StartupException>(() => new ServiceCollection().AddJanus(
                Connection,
                Usable,
                Fingerprints(new byte[32]),
                Encoding.UTF8.GetBytes("the secret this application presents"),
                ReadOnlyMemory<byte>.Empty,
                HostFixture.Declaration(),
                ApplicationKind.Public))
            .Failure;

        Assert.Equal(ErrorCodes.StartupKeyUnavailable, refused?.Code);
        Assert.Equal("maintenanceCredential", refused?.Details["key"].GetString());
    }

    /// <summary>
    /// AUTH-KEY-002, OPS-SEC-001: every store that wraps a personal field is handed the
    /// keys the host passed in, so everything the entry point registers resolves once
    /// the host has declared what is its own to declare (LIB-HOST-001).
    /// </summary>
    [Fact]
    public void AUTH_KEY_002_EveryStoreIsHandedTheKeysTheHostPassedIn()
    {
        IServiceCollection services = new ServiceCollection()
            .AddSingleton<IEvents>(new EventsInMemory())
            .AddSingleton<IMailTransport>(new MailTransportInMemory())
            .AddSingleton<ISmsTransport>(new SmsTransportInMemory())
            .AddSingleton(new AuthenticationAddresses(
                "https://accounts.example.test/signin",
                "https://accounts.example.test"))
            .AddSingleton(new SignOnClient("this-application"))
            .AddJanus(
                Connection,
                Usable,
                Fingerprints(new byte[32]),
                Encoding.UTF8.GetBytes("the secret this application presents"),
                Encoding.UTF8.GetBytes(Connection),
                HostFixture.Declaration(),
                ApplicationKind.Public);

        Assert.Null(Record.Exception(() => services
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true })
            .Dispose()));
    }

    private static KeyEncryptionKeys Usable =>
        new(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] });

    private static FingerprintKeys Fingerprints(byte[] key) =>
        new(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = key });

    private static ErrorCode? Refused(KeyEncryptionKeys? keys, FingerprintKeys? fingerprintKeys) =>
        Assert.Throws<StartupException>(() => new ServiceCollection().AddJanus(
                Connection,
                keys!,
                fingerprintKeys!,
                Encoding.UTF8.GetBytes("the secret this application presents"),
                Encoding.UTF8.GetBytes(Connection),
                HostFixture.Declaration(),
                ApplicationKind.Public))
            .Failure?.Code;
}
