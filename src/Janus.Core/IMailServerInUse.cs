namespace Janus.Core;

/// <summary>
/// The mail server the deployment uses, chosen once, when the application starts, and
/// held until it stops.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-LAYOUT-002, INT-MAIL-001, INT-MAIL-006 and
/// LIB-HOST-001. The choice is the host's own <see cref="IMailServer"/> where it
/// registered one; otherwise the library's shipped adapter where
/// <c>integration.mailserver.endpoint</c> is set when the application starts; otherwise
/// none. The push, reconciliation, the app-password operations, the invitation's mailbox
/// rule and the records of processing ask it, and none of them resolves
/// <see cref="IMailServer"/> itself, so a deployment never shows a mail server in one
/// place and none in another. A change of the key takes effect at the next start. A read
/// before the start made the choice is a fault. No host implements it.
/// </remarks>
public interface IMailServerInUse
{
    /// <summary>
    /// Answers the mail server in use.
    /// </summary>
    /// <returns>
    /// The mail server, or <c>identity.mailbox.notfound</c> where the deployment has none
    /// registered.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// The application's start has not yet chosen.
    /// </exception>
    Result<IMailServer> Chosen();
}
