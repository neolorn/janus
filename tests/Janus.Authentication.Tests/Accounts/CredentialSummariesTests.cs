using System;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// A credential as a caller is shown it (chapter 09 <c>GET /account/credentials</c>).
/// </summary>
[Trait("kind", "unit")]
public sealed class CredentialSummariesTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Person = new(Guid.NewGuid());

    /// <summary>
    /// AUTH-RECOV-007 AC3 and chapter 09 (D-197): a credential that is not suspended
    /// says nothing of a cancel; a suspended one says a cancel returns it, save a text
    /// code's credential whose number left the account, whatever suspended it, and a
    /// social credential a provider's security event holds.
    /// </summary>
    [Fact]
    public void AUTH_RECOV_007_AC3_ASuspendedCredentialSaysWhetherACancelReturnsIt()
    {
        Authenticator active = Texted();
        Authenticator reported = Texted();
        Authenticator unbound = Texted();
        var held = Authenticator.Linked(AuthenticatorId.New(TimeProvider.System), Person, Factor.Google, Label(), Noon);

        reported.Suspend(Noon + TimeSpan.FromDays(7));
        unbound.Suspend(Noon + TimeSpan.FromDays(7));
        unbound.Unbind();
        held.Hold();

        Assert.Null(CredentialSummaries.Of(active, preferred: false).Cancellable);
        Assert.True(CredentialSummaries.Of(reported, preferred: false).Cancellable);
        Assert.False(CredentialSummaries.Of(unbound, preferred: false).Cancellable);
        Assert.False(CredentialSummaries.Of(held, preferred: false).Cancellable);
        Assert.Equal(Noon + TimeSpan.FromDays(7), CredentialSummaries.Of(unbound, preferred: false).InvalidatesAt);
    }

    private static Authenticator Texted() =>
        Authenticator.Bound(
            AuthenticatorId.New(TimeProvider.System),
            Person,
            Factor.PhoneCode,
            Label(),
            "+441632960044",
            Noon);

    private static CredentialLabel Label() =>
        CredentialLabel.TryParse("My phone", out CredentialLabel label)
            ? label
            : throw new Xunit.Sdk.XunitException("The label is one the chapter admits.");
}
