using System;
using System.Linq;
using System.Text;
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
/// The mail app password endpoints of chapter 09 section 6: the mail server is called on
/// the account of the mailbox the signed-in person holds, and the library issues no
/// token for it (REG-MAIL-002, INT-MAIL-010, AUTH-OIDC-001 AC4, D-226).
/// </summary>
[Trait("kind", "unit")]
public sealed class AppPasswordFlowTests : IAsyncDisposable
{
    private const string Path = "/account/mail/apppasswords/";

    private const string Address = "person@staff.example.test";

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser.
    /// </summary>
    public AppPasswordFlowTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// INT-MAIL-010 AC1 to AC3, AUTH-OIDC-001 AC4: an app password is created, listed and
    /// revoked at the server on the account of the mailbox the signed-in person holds,
    /// the library issuing no token for it; the secret is answered once and is cached
    /// nowhere.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_010_AC1_TheServerIsCalledOnTheAccountOfTheMailboxHeldAsync()
    {
        Browser browser = await HolderAsync();
        MailboxId held = Assert.Single(_deployment.Mailboxes.Held).Id;

        Answer created = await browser.SendAsync("POST", Path, ("label", "Phone"));
        Answer listed = await browser.SendAsync("GET", Path);

        Assert.Equal(StatusCodes.Status200OK, created.Status);
        Assert.Equal("no-store", created.Header("Cache-Control"));
        Assert.Equal(Assert.Single(_deployment.MailServer.Secrets), created.Text("secret"));
        Assert.Equal(StatusCodes.Status200OK, listed.Status);
        Assert.Equal("Phone", listed.Json()[0].GetProperty("label").GetString());
        Assert.Equal([held, held], _deployment.MailServer.Calls);
        Assert.Empty(_deployment.Tokens.All);

        Answer revoked = await browser.SendAsync("DELETE", Path + created.Text("id"));

        Assert.Equal(StatusCodes.Status204NoContent, revoked.Status);
        Assert.Empty(_deployment.MailServer.AppPasswordsOf(Address));
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
    /// CONV-DESIGN-006 AC5, 09 section 6 (D-187): an <c>{id}</c> that does not read as an
    /// app-password identifier is malformed naming <c>id</c>, and the server is never
    /// asked; one that reads and names nothing the server holds is not found.
    /// </summary>
    /// <param name="id">The identifier in the path.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("p0001=")]
    [InlineData("p%2B0001")]
    [InlineData("p.0001")]
    [InlineData("%20")]
    public async Task CONV_DESIGN_006_AC5_AnIdentifierOutsideItsFormIsMalformedAsync(string id)
    {
        Browser browser = await HolderAsync();

        Answer refused = await browser.SendAsync("DELETE", Path + id);
        Answer unheld = await browser.SendAsync("DELETE", Path + "p0001");

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), refused.Text("code"));
        Assert.Equal("id", refused.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal(StatusCodes.Status404NotFound, unheld.Status);
        Assert.Equal(ErrorCodes.CredentialNotFound.ToString(), unheld.Text("code"));
        Assert.Single(_deployment.MailServer.Calls);
    }

    /// <summary>
    /// INT-MAIL-006: an account that holds no mailbox has no app passwords to manage,
    /// and a creation without a label is malformed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006_AnAccountWithoutAMailboxIsRefusedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync("GET", Path);
        Answer unlabelled = await browser.SendAsync("POST", Path, ("expiresAt", "2027-01-01T00:00:00Z"));

        Assert.Equal(StatusCodes.Status404NotFound, refused.Status);
        Assert.Equal(ErrorCodes.MailboxNotFound.ToString(), refused.Text("code"));
        Assert.Equal(StatusCodes.Status400BadRequest, unlabelled.Status);
        Assert.Equal("label", unlabelled.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Empty(_deployment.MailServer.Calls);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC4 and chapter 09 section 6: a creation by an account restricted
    /// after the gate step is refused 403 <c>authz.restricted</c> at the second ask,
    /// after the server's call; the password the server created is revoked there and
    /// the answer carries no secret.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC4_ACreationRefusedAtTheSecondAskLeavesNoPasswordAtTheServerAsync()
    {
        Browser browser = await HolderAsync();
        Answer? refused = null;

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            async () => refused = await browser.SendAsync("POST", Path, ("label", "Phone")));

        string secret = Assert.Single(_deployment.MailServer.Secrets);

        Assert.Empty(_deployment.MailServer.AppPasswordsOf(Address));
        Assert.DoesNotContain(secret, Assert.IsType<Answer>(refused).Body, StringComparison.Ordinal);
        Assert.DoesNotContain(_deployment.Logs.Lines, line => line.Contains(secret, StringComparison.Ordinal));
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC4 and INT-MAIL-010: where the server does not take the
    /// revocation, the creation is still refused 403 <c>authz.restricted</c> with no
    /// secret, the failure is logged by the holder and the code alone, and the password
    /// stays listed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC4_ARevocationTheServerDoesNotTakeIsLoggedAndStillRefusedAsync()
    {
        Browser browser = await HolderAsync();
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        Answer? refused = null;

        _deployment.MailServer.Created = () => _deployment.MailServer.Unreachable = true;

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            async () => refused = await browser.SendAsync("POST", Path, ("label", "Phone")));

        _deployment.MailServer.Unreachable = false;

        string secret = Assert.Single(_deployment.MailServer.Secrets);

        Assert.Equal("Phone", Assert.Single(_deployment.MailServer.AppPasswordsOf(Address)).Label);
        Assert.DoesNotContain(secret, Assert.IsType<Answer>(refused).Body, StringComparison.Ordinal);
        Assert.Contains(
            _deployment.Logs.Lines,
            line => line.Contains(subject.ToString(), StringComparison.Ordinal)
                && line.Contains(ErrorCodes.SystemFault.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(_deployment.Logs.Lines, line => line.Contains(secret, StringComparison.Ordinal));
    }

    // INT-MAIL-006: the browser's account holds the mailbox its membership of the
    // administrative organization gave it, which the server hosts.
    private async Task<Browser> HolderAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        var mailbox = Mailbox.Reserved(Parsed(Address), _deployment.Clock.GetUtcNow());

        mailbox.Hold(_deployment.Directory.Created[^1].Subject);
        _deployment.Mailboxes.Held.Add(mailbox);
        _deployment.MailServer.Set(Address, enabled: true, mailbox.Id);

        return browser;
    }

    private static EmailAddress Parsed(string value)
    {
        Assert.True(EmailAddress.TryParse(value, out EmailAddress address));

        return address;
    }
}
