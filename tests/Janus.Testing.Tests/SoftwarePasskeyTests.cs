using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Janus.Testing.Tests;

/// <summary>
/// The software passkey's private key, cleared when the passkey is disposed
/// (LIB-TEST-003, CONV-CODE-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class SoftwarePasskeyTests : IDisposable
{
    private static readonly Uri Origin = new("https://identity.example.test");

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// LIB-TEST-003, CONV-CODE-007: a passkey that answered a sign-in's request options
    /// answers none once it is disposed, since its private key is cleared, and creates no
    /// credential either.
    /// </summary>
    [Fact]
    public void LIB_TEST_003_ADisposedPasskeyAnswersNoCeremonyAsItsKeyIsCleared()
    {
        using var passkey = new SoftwarePasskey(Origin, _randomness);

        _ = passkey.Create(Options(creation: true));
        _ = passkey.Get(Options(creation: false));

        passkey.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(() => passkey.Get(Options(creation: false)));
        _ = Assert.Throws<ObjectDisposedException>(() => passkey.Create(Options(creation: true)));
    }

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    // The options the library answers for a ceremony, with what the passkey reads of them.
    private static JsonElement Options(bool creation)
    {
        string challenge = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        string options = creation
            ? "{\"challenge\":\"" + challenge + "\",\"relyingPartyId\":\"identity.example.test\","
                + "\"user\":{\"id\":\"" + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)) + "\"},"
                + "\"algorithms\":[-7]}"
            : "{\"challenge\":\"" + challenge + "\",\"relyingPartyId\":\"identity.example.test\",\"allowCredentials\":[]}";

        return JsonSerializer.Deserialize<JsonElement>(options);
    }
}
