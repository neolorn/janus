using System;
using System.Linq;
using System.Security.Cryptography;
using Janus.Authentication.SignIn;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.SignIn;

/// <summary>
/// A sign-in link or code that has gone out, and the credential a second step's code
/// names from its issue (AUTH-FACT-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class PendingSignInTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// AUTH-FACT-004 AC7: a second step's code names, from its issue, the credential it
    /// is issued for.
    /// </summary>
    [Fact]
    public void AUTH_FACT_004_AC7_ASecondStepCodeNamesFromItsIssueTheCredentialItIsIssuedFor()
    {
        var credential = new AuthenticatorId(Guid.CreateVersion7());

        PendingSignIn issued = Issued(Factor.PhoneCode, credential);

        Assert.Equal(credential, issued.Credential);
    }

    /// <summary>
    /// AUTH-FACT-004: the entries whose code names a credential are the second steps the
    /// library texts, which is the text code alone.
    /// </summary>
    [Fact]
    public void NamesCredential_EveryCatalogueEntry_OnlyTheTextedSecondStep()
    {
        Factor[] naming = [.. Enum.GetValues<Factor>().Where(PendingSignIn.NamesCredential)];

        Assert.Equal([Factor.PhoneCode], naming);
    }

    /// <summary>
    /// AUTH-FACT-004: a second step's code that names no credential is never issued.
    /// </summary>
    [Fact]
    public void Issue_ASecondStepCodeNamingNoCredential_Throws() =>
        Assert.Throws<ArgumentException>(() => Issued(Factor.PhoneCode, credential: null));

    /// <summary>
    /// AUTH-FACT-004: a link or code of an entry that is no second step is issued for no
    /// credential, and one naming a credential is never issued.
    /// </summary>
    /// <param name="factor">The entry.</param>
    [Theory]
    [InlineData(Factor.EmailLink)]
    [InlineData(Factor.EmailCode)]
    [InlineData(Factor.PhoneLink)]
    public void Issue_AnotherEntryNamingACredential_Throws(Factor factor)
    {
        var credential = new AuthenticatorId(Guid.CreateVersion7());

        PendingSignIn issued = Issued(factor, credential: null);

        Assert.Null(issued.Credential);
        Assert.Throws<ArgumentException>(() => Issued(factor, credential));
    }

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    private PendingSignIn Issued(Factor factor, AuthenticatorId? credential) =>
        PendingSignIn.Issue(
            OpaqueToken.Draw(_randomness),
            new SubjectId(Guid.NewGuid()),
            factor,
            email: null,
            credential,
            "123456",
            browser: null,
            challenge: null,
            Noon,
            TimeSpan.FromMinutes(10));
}
