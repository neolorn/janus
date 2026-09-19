using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Passwords;

/// <summary>
/// The word list the dictionary rejection source reads, where a host has enabled one.
/// </summary>
/// <remarks>
/// Implements AUTH-PASS-004. The source is off by default, a recorded deviation from
/// NIST SP 800-63B-4 section 3.1.1.2; a host that turns it on and supplies no list
/// gets a refusal rather than an unscreened password.
/// </remarks>
internal interface IWordList
{
    /// <summary>
    /// Whether a listed word appears in the password.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether one appears, or the failure where the list could not be read.
    /// </returns>
    ValueTask<Result<bool>> MatchesAsync(string password, CancellationToken cancellationToken);
}
