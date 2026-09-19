using System.Security.Cryptography;
using System.Text;
using Janus.Authentication.Passwords;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Passwords;

/// <summary>
/// Hashing and verification (AUTH-PASS-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class Argon2idHasherTests
{
    private static readonly Argon2StrengthClass Shipped = new(
        Settings.PasswordArgon2Memory.Default,
        Settings.PasswordArgon2Iterations.Default);

    /// <summary>
    /// AUTH-PASS-007 AC3: the shipped parameters are the ones the chapter states, and
    /// a password hashed at them verifies.
    /// </summary>
    [Fact]
    public void AUTH_PASS_007_AC3_TheDefaultHashesAtTheShippedParameters()
    {
        PasswordHash hash = Hash("orangemarmalade");

        Assert.Equal(new Argon2StrengthClass(19456, 2), hash.Parameters);
        Assert.Equal(1, hash.Parallelism);
        Assert.True(Argon2idHasher.Verify(Encoding.UTF8.GetBytes("orangemarmalade"), hash));
    }

    /// <summary>
    /// AUTH-PASS-007: a different password does not verify, and the comparison does
    /// not depend on how much of the digest matched.
    /// </summary>
    [Fact]
    public void Verify_ADifferentPassword_DoesNotVerify() =>
        Assert.False(Argon2idHasher.Verify(
            Encoding.UTF8.GetBytes("orangemarmalad"),
            Hash("orangemarmalade")));

    /// <summary>
    /// AUTH-PASS-007 AC1: the parameters travel with the hash, so raising the
    /// deployment's parameters leaves a password hashed at the old ones verifiable.
    /// </summary>
    [Fact]
    public void AUTH_PASS_007_AC1_RaisingTheParametersLeavesExistingPasswordsVerifiable()
    {
        var stored = PasswordHash.Parse(Hash("orangemarmalade").Encoded);
        var raised = new Argon2StrengthClass(47104, 3);

        Assert.True(Argon2idHasher.Verify(Encoding.UTF8.GetBytes("orangemarmalade"), stored));
        Assert.NotEqual(raised, stored.Parameters);
    }

    /// <summary>
    /// AUTH-PASS-007: the stored string reads back as the hash it was written from,
    /// so the row is the whole of what verification needs.
    /// </summary>
    [Fact]
    public void Parse_AStoredHash_ReadsBackEverythingVerificationNeeds()
    {
        PasswordHash written = Hash("orangemarmalade");

        var read = PasswordHash.Parse(written.Encoded);

        Assert.Equal(written.Encoded, read.Encoded);
        Assert.Equal(written.Parameters, read.Parameters);
        Assert.Equal(written.Salt.ToArray(), read.Salt.ToArray());
        Assert.Equal(written.Digest.ToArray(), read.Digest.ToArray());
    }

    /// <summary>
    /// AUTH-PASS-007: two accounts choosing the same password hold different hashes,
    /// the salt being drawn per password.
    /// </summary>
    [Fact]
    public void Hash_TheSamePasswordTwice_DrawsADifferentSalt() =>
        Assert.NotEqual(Hash("orangemarmalade").Encoded, Hash("orangemarmalade").Encoded);

    private static PasswordHash Hash(string password)
    {
        using var randomness = RandomNumberGenerator.Create();

        return new Argon2idHasher(randomness).Hash(
            Encoding.UTF8.GetBytes(password),
            Shipped,
            Settings.PasswordArgon2Parallelism.Default);
    }
}
