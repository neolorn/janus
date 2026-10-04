using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The mail server in use: chosen once, and a fault to ask before the choice
/// (CONV-DESIGN-007, D-176).
/// </summary>
[Trait("kind", "unit")]
public sealed class MailServerInUseTests
{
    /// <summary>
    /// CONV-DESIGN-007 AC5: a read before the start chose is a fault, and the choice is
    /// made once.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC5_AReadBeforeTheChoiceThrowsAndTheChoiceIsMadeOnce()
    {
        var inUse = new MailServerInUse();

        Assert.Throws<InvalidOperationException>(() => inUse.Chosen());

        inUse.Choose(server: null);

        Assert.Equal(ErrorCodes.MailboxNotFound, inUse.Chosen().Match<ErrorCode?>(_ => null, error => error.Code));
        Assert.Throws<InvalidOperationException>(() => inUse.Choose(new Unreached()));
    }

    /// <summary>
    /// CONV-DESIGN-007 AC5: once chosen, the mail server is the one the start chose.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC5_TheChosenMailServerIsAnswered()
    {
        var inUse = new MailServerInUse();
        var server = new Unreached();

        inUse.Choose(server);

        Assert.Same(server, inUse.Chosen().Match<IMailServer?>(chosen => chosen, _ => null));
    }

    // A mail server the tests choose and never call.
    private sealed class Unreached : IMailServer
    {
        public ValueTask<Result> ProvisionAsync(MailboxPush push, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The mail server is not called here.");

        public ValueTask<Result<IReadOnlyList<HostedMailbox>>> MailboxesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The mail server is not called here.");

        public ValueTask<Result<IReadOnlyList<AppPassword>>> AppPasswordsAsync(
            string accessToken,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The mail server is not called here.");

        public ValueTask<Result<IssuedAppPassword>> CreateAppPasswordAsync(
            string accessToken,
            string label,
            DateTimeOffset? expiresAt,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The mail server is not called here.");

        public ValueTask<Result> RevokeAppPasswordAsync(
            string accessToken,
            AppPasswordId id,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The mail server is not called here.");
    }
}
