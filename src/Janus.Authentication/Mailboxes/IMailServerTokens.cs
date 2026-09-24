using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Mailboxes;

/// <summary>
/// Where the library obtains a token for the signed-in person through its first-party
/// client for the mail server, which is what the mail server's app-password calls
/// carry.
/// </summary>
/// <remarks>
/// Implements INT-MAIL-010 and AUTH-OIDC-001 AC4. The provider is the library's own, so
/// the token is issued in process from the person's session record, as the token
/// endpoint would issue one to that client, and is kept nowhere.
/// </remarks>
internal interface IMailServerTokens
{
    /// <summary>
    /// Issues an access token to the mail server's client for the person the session
    /// is.
    /// </summary>
    /// <param name="subject">Whose token.</param>
    /// <param name="session">The session it stands on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The token, or <c>auth.session.expired</c> where the session no longer answers,
    /// <c>authz.denied</c> where it is not the subject's.
    /// </returns>
    ValueTask<Result<string>> IssueAsync(
        SubjectId subject,
        SessionId session,
        CancellationToken cancellationToken);
}
