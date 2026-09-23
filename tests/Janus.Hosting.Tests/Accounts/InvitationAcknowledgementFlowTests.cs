using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Accounts;
using Janus.Authentication.Invitations;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Accounts;

/// <summary>
/// The membership step of an account an invitation is attached to (chapter 09
/// section 6a, REG-INV-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class InvitationAcknowledgementFlowTests : IAsyncDisposable
{
    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to send, which is what registering a browser needs.
    /// </summary>
    public InvitationAcknowledgementFlowTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REG-INV-002: the membership step reads the invitation attached to the account:
    /// the organization, who invited them, the roles and the documents at their
    /// versions; an account with none attached is answered with its code.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_INV_002_TheMembershipStepReadsTheAttachedInvitationAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId holder = _deployment.Directory.Created[^1].Subject;

        Answer none = await browser.SendAsync("GET", "/account/invitation");

        Assert.Equal(StatusCodes.Status404NotFound, none.Status);
        Assert.Equal(ErrorCodes.InvitationNotFound.ToString(), none.Text("code"));

        Invitation invitation = Attached(holder);

        Answer read = await browser.SendAsync("GET", "/account/invitation");
        JsonElement shown = read.Json();
        JsonElement document = shown.GetProperty("documents")[0];

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal(invitation.Id.Value.ToString(), read.Text("id"));
        Assert.Equal(invitation.Organization.Value.ToString(), read.Text("organization"));
        Assert.Equal("Northern branch", read.Text("organizationName"));
        Assert.Equal("Ada", read.Text("invitedBy"));
        Assert.Equal("clerk", shown.GetProperty("roles")[0].GetString());
        Assert.Equal("staff-handbook", document.GetProperty("document").GetString());
        Assert.Equal("2", document.GetProperty("version").GetString());
    }

    // An invitation into a named organization, from an account that shows its name,
    // attached to the account that opened its link.
    private Invitation Attached(SubjectId holder)
    {
        using var randomness = RandomNumberGenerator.Create();

        var organization = OrganizationId.New(TimeProvider.System);
        var inviter = SubjectId.New(randomness);
        DateTimeOffset now = _deployment.Clock.GetUtcNow();

        _deployment.Organizations.Seed(organization, name: "Northern branch");
        _deployment.Accounts.Holds(
            inviter,
            new HeldProfile(
                DisplayName.TryParse("Ada", out DisplayName named) ? named : null,
                LegalName: null,
                DateOfBirth: null,
                PhotoUpdatedAt: null));

        var invitation = Invitation.Issued(
            InvitationId.New(TimeProvider.System),
            organization,
            inviter,
            new InvitedIdentifiers(Email: null, Phone: null, CorporateEmail: null),
            [RoleName.Parse("clerk")],
            [new InvitationDocument("staff-handbook", "2")],
            mailbox: null,
            OpaqueToken.Draw(randomness).Fingerprint(),
            now,
            TimeSpan.FromDays(7));

        invitation.AttachTo(holder, now);
        _deployment.Invitations.Held.Add(invitation);

        return invitation;
    }
}
