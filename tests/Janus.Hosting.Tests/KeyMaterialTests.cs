using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Oidc;
using Janus.Hosting.Tests.Authorization;
using Janus.Privacy.SubjectKeys;
using Janus.Privacy.Tests.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// What the library does when the values the secrets manager holds are not there to be
/// had, and when a key is lent (AUTH-KEY-002, OPS-SEC-001, CONV-CODE-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class KeyMaterialTests
{
    private const string Connection = "Host=nowhere;Database=identity";

    // The head every library assembly's name carries, read from the core's
    // namespace so no string spells the product name (CONV-NAME-001).
    private static readonly string Library = typeof(Result).Namespace!.Split('.')[0] + ".";

    /// <summary>
    /// AUTH-KEY-002 AC2: without the key-encryption key the library does not start,
    /// and what stops it names why.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_002_AC2_StartupFailsNamedWithoutTheKeyEncryptionKeyAsync()
    {
        Error? refused = await RefusedAsync(new SecretSourceInMemory(None) { KeyEncryptionKeys = null });

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused?.Code);
        Assert.Equal("keyEncryptionKeys", refused?.Details["key"].GetString());
    }

    /// <summary>
    /// AUTH-KEY-002 AC2: the fingerprint key is read from the same place and the
    /// library starts without it no more than it starts without the other.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_002_AC2_StartupFailsNamedWithoutTheFingerprintKeyAsync()
    {
        Error? refused = await RefusedAsync(new SecretSourceInMemory(None) { FingerprintKeys = null });

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused?.Code);
        Assert.Equal("fingerprintKeys", refused?.Details["key"].GetString());
    }

    /// <summary>
    /// AUTH-KEY-002 AC2: a fingerprint key shorter than the hash it computes is no
    /// key, and is refused as one that is absent is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_002_AC2_StartupFailsNamedOnAFingerprintKeyShorterThanTheHashAsync()
    {
        Error? refused = await RefusedAsync(new SecretSourceInMemory(None) { FingerprintKeys = Fingerprints(new byte[16]) });

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused?.Code);
        Assert.Equal("fingerprintKeys", refused?.Details["key"].GetString());
    }

    /// <summary>
    /// AUTH-KEY-002 AC2, OPS-SEC-003: a version retained beside the current one is read
    /// as the current one is, and is refused when it is short as the current one is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_002_AC2_StartupFailsNamedOnARetainedFingerprintKeyShorterThanTheHashAsync()
    {
        Error? refused = await RefusedAsync(new SecretSourceInMemory(None)
        {
            FingerprintKeys = new FingerprintKeys(
                2,
                new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[16], [2] = new byte[32] }),
        });

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused?.Code);
        Assert.Equal("fingerprintKeys", refused?.Details["key"].GetString());
    }

    /// <summary>
    /// OPS-MIG-003a, PRIV-RET-002: the maintenance credential is read from the same place,
    /// and the library does not start without it, or with it empty, since without it the
    /// audit trail stops taking rows once the months created ahead have passed.
    /// </summary>
    /// <param name="answeredEmpty">Whether the source answers it empty rather than not at all.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OPS_MIG_003a_StartupFailsNamedWithoutTheMaintenanceCredentialAsync(bool answeredEmpty)
    {
        Error? refused = await RefusedAsync(new SecretSourceInMemory(None) { MaintenanceCredential = answeredEmpty ? [] : null });

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused?.Code);
        Assert.Equal("maintenanceCredential", refused?.Details["key"].GetString());
    }

    /// <summary>
    /// CONV-CODE-007 AC3, CONV-DESIGN-007: no secret is an argument of the entry point
    /// and no service is handed a key when it is registered or made, so everything the
    /// entry point registers resolves once the host has declared what is its own to
    /// declare (LIB-HOST-001), and no registration holds or takes a key.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_AC3_NoServiceReceivesAKeyAtRegistration()
    {
        IServiceCollection services = Registered(new SecretSourceInMemory(None));

        Assert.Null(Record.Exception(() => services
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true })
            .Dispose()));
        Assert.DoesNotContain(
            services,
            service => !service.IsKeyedService && service.ImplementationInstance is KeyEncryptionKeys or FingerprintKeys);
        Assert.DoesNotContain(
            services
                .Select(Made)
                .OfType<Type>()
                .Where(type => type.Assembly.GetName().Name?.StartsWith(Library, StringComparison.Ordinal) is true)
                .SelectMany(type => type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                .SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(KeyEncryptionKeys)
                || parameter.ParameterType == typeof(FingerprintKeys));
    }

    /// <summary>
    /// OPS-SEC-001 AC2 (D-183): where a subject key that is not erased stands under a
    /// key-encryption key version the source does not supply, the start is refused naming
    /// the lowest such version; an erased key under such a version refuses nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_001_AC2_StartupFailsNamedWhereALiveSubjectKeyStandsUnderAVersionTheSourceLacksAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var keys = new SubjectKeyStoreInMemory();
        var erased = SubjectKey.Wrapped(new SubjectKeyId(Guid.NewGuid()), 1, new byte[40]);

        erased.Erase();
        keys.Hold(erased);
        keys.Hold(SubjectKey.Wrapped(new SubjectKeyId(Guid.NewGuid()), 4, new byte[40]));
        keys.Hold(SubjectKey.Wrapped(new SubjectKeyId(Guid.NewGuid()), 3, new byte[40]));
        keys.Hold(SubjectKey.Wrapped(new SubjectKeyId(Guid.NewGuid()), 5, new byte[40]));

        await using ServiceProvider deployed = Registered(new SecretSourceInMemory(None)
        {
            KeyEncryptionKeys = new KeyEncryptionKeys(5, new Dictionary<int, ReadOnlyMemory<byte>>
            {
                [5] = new byte[32],
            }),
        })
            .AddSingleton<ISubjectKeyStore>(keys)
            .BuildServiceProvider();

        KeyRingService service = deployed.GetServices<IHostedService>().OfType<KeyRingService>().Single();

        await service.StartingAsync(cancellationToken);

        Error? refused = (await Assert.ThrowsAsync<StartupException>(
                async () => await service.StartAsync(cancellationToken)))
            .Failure;

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused?.Code);
        Assert.Equal("keyEncryptionKeys", refused?.Details["key"].GetString());
        Assert.Equal(3, refused?.Details["version"].GetInt32());
    }

    /// <summary>
    /// CONV-CODE-007 AC3, OPS-SEC-001: the keys are read into the ring as the application
    /// starts, so a read before the start is a fault; the ring lends what the source
    /// answered from then on, and once the application has stopped a read is a fault
    /// again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_007_AC3_AKeyIsLentOnlyFromTheStartUntilTheStopAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        byte[] current = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

        await using ServiceProvider deployed = Registered(new SecretSourceInMemory(None)
        {
            KeyEncryptionKeys = new KeyEncryptionKeys(2, new Dictionary<int, ReadOnlyMemory<byte>>
            {
                [1] = new byte[32],
                [2] = current,
            }),
        }).BuildServiceProvider();

        IKeyRing ring = deployed.GetRequiredService<IKeyRing>();
        KeyRingService service = deployed.GetServices<IHostedService>().OfType<KeyRingService>().Single();

        Assert.Throws<InvalidOperationException>(() => ring.BorrowKeyEncryptionKeys(keys => keys.CurrentVersion));

        await service.StartingAsync(cancellationToken);

        Assert.Equal(current, ring.BorrowKeyEncryptionKey(2, key => key.ToArray()).Match(key => key, _ => []));
        Assert.Equal(
            "Host=maintenance.example.test"u8.ToArray(),
            ring.BorrowMaintenanceCredential(credential => credential.ToArray()).Match(credential => credential, _ => []));

        Error? retired = ring.BorrowKeyEncryptionKey(3, key => key.Length).Match(_ => (Error?)null, error => error);

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, retired?.Code);
        Assert.Equal("keyEncryptionKeys", retired?.Details["key"].GetString());
        Assert.Equal(3, retired?.Details["version"].GetInt32());

        await service.StoppedAsync(cancellationToken);

        Assert.Throws<InvalidOperationException>(() => ring.BorrowKeyEncryptionKeys(keys => keys.CurrentVersion));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowFingerprintKeys(keys => keys.CurrentVersion));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowMaintenanceCredential(credential => credential.Length));
    }

    /// <summary>
    /// CONV-CODE-007 AC4, AUTH-KEY-002: the provider's encryption credential is made from
    /// every version the ring holds, the current first, only once the start has filled
    /// the ring; the credential holds a copy of its own, since the bytes it was made from
    /// are cleared as the making returns and it still holds the key derived.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_007_AC4_TheEncryptionCredentialIsMadeFromTheFilledRingAsync()
    {
        byte[] previous = [.. Enumerable.Repeat((byte)1, 32)];
        byte[] current = [.. Enumerable.Repeat((byte)2, 32)];

        await using ServiceProvider deployed = Registered(new SecretSourceInMemory(None)
        {
            KeyEncryptionKeys = new KeyEncryptionKeys(2, new Dictionary<int, ReadOnlyMemory<byte>>
            {
                [1] = previous,
                [2] = current,
            }),
        }).BuildServiceProvider();

        IKeyRing ring = deployed.GetRequiredService<IKeyRing>();

        Assert.Throws<InvalidOperationException>(() => TokenProtection.Keys(ring));

        await deployed.GetServices<IHostedService>().OfType<KeyRingService>().Single()
            .StartingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [Derived(current), Derived(previous)],
            TokenProtection.Keys(ring).Select(key => key.Key));
    }

    // AUTH-KEY-002: what a version derives under the token-protection purpose.
    private static byte[] Derived(byte[] version) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            version,
            32,
            salt: [],
            info: Encoding.UTF8.GetBytes("identity:oidc:token-protection:v1"));

    // No social provider's credential: the deployment declares none.
    private static Dictionary<string, ProviderCredential> None => new(StringComparer.Ordinal);

    private static FingerprintKeys Fingerprints(byte[] key) =>
        new(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = key });

    private static IServiceCollection Registered(SecretSourceInMemory source) =>
        new ServiceCollection()
            .AddSingleton<IMailTransport>(new MailTransportInMemory())
            .AddSingleton<ISmsTransport>(new SmsTransportInMemory())
            .AddSingleton(new AuthenticationAddresses(
                "https://accounts.example.test/signin",
                "https://accounts.example.test"))
            .AddSingleton(Landing.Origins)
            .AddSingleton(new SignOnClient("this-application"))
            .AddSingleton<ISecretSource>(source)
            .AddJanus(Connection, HostFixture.Declaration(), ApplicationKind.Public);

    // The type a registration makes: the one it names, the instance it holds, or what its
    // factory is declared to answer.
    private static Type? Made(ServiceDescriptor service) =>
        service.IsKeyedService
            ? service.KeyedImplementationType
                ?? service.KeyedImplementationInstance?.GetType()
                ?? service.KeyedImplementationFactory?.Method.ReturnType
            : service.ImplementationType
                ?? service.ImplementationInstance?.GetType()
                ?? service.ImplementationFactory?.Method.ReturnType;

    // The start as the ring's hosted service begins it, and the failure it stops with.
    private static async Task<Error?> RefusedAsync(SecretSourceInMemory source)
    {
        await using ServiceProvider deployed = Registered(source).BuildServiceProvider();

        KeyRingService service = deployed.GetServices<IHostedService>().OfType<KeyRingService>().Single();

        return (await Assert.ThrowsAsync<StartupException>(
                async () => await service.StartingAsync(TestContext.Current.CancellationToken)))
            .Failure;
    }
}
