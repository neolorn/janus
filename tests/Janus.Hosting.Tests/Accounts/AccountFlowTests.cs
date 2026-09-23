using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Accounts;

/// <summary>
/// What a signed-in browser reads and changes about its own account, and what it is
/// told about a record that is not its own (REG-ACCT-001, API-CONV-003, FE-ACCT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccountFlowTests : IAsyncDisposable
{
    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to send, which is what registering a browser needs.
    /// </summary>
    public AccountFlowTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REG-ACCT-001 AC1: one read carries the identifiers, the credentials, the
    /// profile and the preferences of the account the browser signed in on.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_ACCT_001_AC1_TheAccountReadsItsOwnGroupsAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer account = await browser.SendAsync("GET", "/account");
        JsonElement read = account.Json();

        Assert.Equal(StatusCodes.Status200OK, account.Status);
        Assert.Equal("active", account.Text("state"));
        Assert.Equal(JsonValueKind.Object, read.GetProperty("profile").ValueKind);
        Assert.Equal(JsonValueKind.Object, read.GetProperty("preferences").ValueKind);
        Assert.Equal(JsonValueKind.Array, read.GetProperty("credentials").ValueKind);

        Assert.Equal(
            Flow.Address,
            read.GetProperty("identifiers").GetProperty("emails")[0].GetProperty("value").GetString());

        Assert.Equal(
            Flow.Number,
            read.GetProperty("identifiers").GetProperty("phones")[0].GetProperty("value").GetString());
    }

    /// <summary>
    /// API-CONV-003 AC1: a credential that belongs to somebody else is answered
    /// exactly as one that does not exist, so the answer says nothing about which it
    /// was.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_003_AC1_AConcealedDenialReadsAsAnAbsentRecordAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer somebody = await browser.SendAsync(
            "PATCH",
            "/account/credentials/" + Elsewhere(),
            ("label", "This laptop"));

        Answer nobody = await browser.SendAsync(
            "PATCH",
            "/account/credentials/" + Guid.NewGuid(),
            ("label", "This laptop"));

        Assert.Equal(StatusCodes.Status404NotFound, somebody.Status);
        Assert.Equal(StatusCodes.Status404NotFound, nobody.Status);
        Assert.Equal(Without(nobody), Without(somebody));
    }

    // The body without the correlation identifier, which differs between any two
    // requests by design (API-CONV-002 AC2).
    private static string Without(Answer answer) =>
        answer.Body.Replace(answer.Text("correlationId"), string.Empty, StringComparison.Ordinal);

    // A credential of an account that is not the one asking.
    private Guid Elsewhere()
    {
        using var randomness = RandomNumberGenerator.Create();

        var held = Authenticator.WebAuthnCredential(
            AuthenticatorId.New(_deployment.Clock),
            SubjectId.New(randomness),
            Factor.Passkey,
            Labelled("Their laptop"),
            new WebAuthnMaterial(
                new byte[] { 1, 2, 3 },
                new byte[] { 4, 5, 6 },
                Algorithm: -7,
                "identity.example.test",
                Counter: 0,
                BackupEligible: true,
                BackupState: true),
            _deployment.Clock.GetUtcNow());

        _deployment.Authenticators.Hold(held);

        return held.Id.Value;
    }

    private static CredentialLabel Labelled(string label) =>
        CredentialLabel.TryParse(label, out CredentialLabel named)
            ? named
            : throw new InvalidOperationException("The label does not parse.");
}
