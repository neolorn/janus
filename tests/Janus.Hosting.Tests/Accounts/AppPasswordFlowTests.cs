using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Mailboxes;
using Janus.Core;
using Janus.Hosting.Accounts;
using Janus.Hosting.Tests.Oidc;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Accounts;

/// <summary>
/// The mail app password endpoints of chapter 09 section 6, over the provider's own
/// token issuance: the token the mail server is handed is the signed-in person's,
/// issued to the mail server's client, and accepted where the provider's tokens are
/// (REG-MAIL-002, INT-MAIL-010, AUTH-OIDC-001 AC4).
/// </summary>
[Trait("kind", "unit")]
public sealed class AppPasswordFlowTests : IAsyncDisposable
{
    private const string Path = "/account/mail/apppasswords/";

    private const string MailClient = "mail-server";

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, whose registry holds the mail server's
    /// client.
    /// </summary>
    public AppPasswordFlowTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// INT-MAIL-010 AC1 to AC3, AUTH-OIDC-001 AC4: an app password is created, listed and
    /// revoked at the server with a token that names the signed-in person, is issued to
    /// the mail server's client, and is one the provider's own validation accepts; the
    /// secret is answered once and is cached nowhere.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_010_AC1_TheServerIsCalledWithThePersonsTokenAsync()
    {
        Browser browser = await HolderAsync();
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        Answer created = await browser.SendAsync("POST", Path, ("label", "Phone"));
        Answer listed = await browser.SendAsync("GET", Path);

        string token = _deployment.MailServer.Tokens[0];

        Assert.Equal(StatusCodes.Status200OK, created.Status);
        Assert.Equal("no-store", created.Header("Cache-Control"));
        Assert.Equal(Assert.Single(_deployment.MailServer.Secrets), created.Text("secret"));
        Assert.Equal(StatusCodes.Status200OK, listed.Status);
        Assert.Equal("Phone", listed.Json()[0].GetProperty("label").GetString());
        Assert.Equal(subject.ToString(), Claim(token, "sub"));
        Assert.Equal(MailClient, Claim(token, "client_id"));
        Assert.Equal(
            StatusCodes.Status200OK,
            (await new Machine(_deployment).GetAsync("/oidc/userinfo", token)).Status);

        Answer revoked = await browser.SendAsync("DELETE", Path + created.Text("id"));

        Assert.Equal(StatusCodes.Status204NoContent, revoked.Status);
        Assert.Empty(_deployment.MailServer.AppPasswordsOf(subject));
    }

    /// <summary>
    /// AUTH-OIDC-006 AC3: the token the mail server is handed is typed `at+jwt`, carries
    /// the seven claims with the mail server's client as its audience, and is taken by
    /// the mail server's adapter.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC3_TheMailServersTokenIsOneItsAdapterTakesAsync()
    {
        Browser browser = await HolderAsync();

        _ = await browser.SendAsync("GET", Path);

        string token = Assert.Single(_deployment.MailServer.Tokens);
        JsonElement claims = MailServerAdapter.Claims(token);

        Assert.Equal("at+jwt", MailServerAdapter.Header(token).GetProperty("typ").GetString());
        Assert.Subset(
            MailServerAdapter.Named(token).ToHashSet(StringComparer.Ordinal),
            MailServerAdapter.Required.ToHashSet(StringComparer.Ordinal));
        Assert.Equal(MailClient, claims.GetProperty("aud").GetString());
        Assert.NotEmpty(claims.GetProperty("jti").GetString()!);
        Assert.True((await MailServerAdapter.VerifyAsync(_deployment, token, MailClient)).IsValid);
        Assert.False((await MailServerAdapter.VerifyAsync(_deployment, token, "another-client")).IsValid);
    }

    /// <summary>
    /// INT-MAIL-010 AC4: the secret the server generated is answered once and reaches no
    /// log line at any level, whoever writes it; the types that carry it are marked
    /// never logged, so a logging call handed one fails the build (JAN0002).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_010_AC4_NoSecretReachesALogAsync()
    {
        Browser browser = await HolderAsync();

        Answer created = await browser.SendAsync("POST", Path, ("label", "Phone"));
        Answer listed = await browser.SendAsync("GET", Path);
        string secret = created.Text("secret");

        Assert.Equal(Assert.Single(_deployment.MailServer.Secrets), secret);
        Assert.DoesNotContain(secret, listed.Body, StringComparison.Ordinal);
        Assert.NotEmpty(_deployment.Logs.Lines);
        Assert.DoesNotContain(_deployment.Logs.Lines, line => line.Contains(secret, StringComparison.Ordinal));
        Assert.All(
            [typeof(IssuedAppPassword), typeof(IssuedAppPasswordView)],
            carrier =>
            {
                Assert.True(carrier.IsDefined(typeof(NeverLoggedAttribute), inherit: false));
                Assert.True(carrier.GetProperty("Secret")!.IsDefined(typeof(NeverLoggedAttribute), inherit: false));
            });
    }

    /// <summary>
    /// INT-MAIL-006: an account that holds no mailbox has no app passwords to manage,
    /// and a creation without a label is malformed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006_AnAccountWithoutAMailboxIsRefusedAsync()
    {
        await RegisteredAsync();

        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync("GET", Path);
        Answer unlabelled = await browser.SendAsync("POST", Path, ("expiresAt", "2027-01-01T00:00:00Z"));

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(StatusCodes.Status400BadRequest, unlabelled.Status);
        Assert.Equal("label", unlabelled.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Empty(_deployment.MailServer.Tokens);
    }

    private static string Claim(string token, string name) =>
        JsonDocument
            .Parse(System.Buffers.Text.Base64Url.DecodeFromChars(token.Split('.')[1]))
            .RootElement
            .GetProperty(name)
            .GetString() ?? string.Empty;

    private async Task RegisteredAsync() =>
        await _deployment.Clients.RecordAsync(
            new OidcClient(
                MailClient,
                MailClient,
                OidcClientKind.Protocol,
                "https://mail.example.test/callback",
                ["openid", "email", "offline_access"]),
            OpaqueToken.Of("the-mail-servers-secret").Fingerprint(),
            DateTimeOffset.MinValue,
            TestContext.Current.CancellationToken);

    // INT-MAIL-006: the browser's account holds the mailbox its membership of the
    // administrative organization gave it.
    private async Task<Browser> HolderAsync()
    {
        await RegisteredAsync();

        Browser browser = await Flow.SignedInAsync(_deployment);
        var mailbox = Mailbox.Reserved(Parsed("person@staff.example.test"), _deployment.Clock.GetUtcNow());

        mailbox.Hold(_deployment.Directory.Created[^1].Subject);
        _deployment.Mailboxes.Held.Add(mailbox);

        return browser;
    }

    private static EmailAddress Parsed(string value)
    {
        Assert.True(EmailAddress.TryParse(value, out EmailAddress address));

        return address;
    }
}
