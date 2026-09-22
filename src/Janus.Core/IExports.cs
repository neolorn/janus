using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The one export routine: what an account asks for to read what is held about it,
/// and to take it elsewhere.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, PRIV-RIGHT-003 and chapter 09 section 7. Access and
/// portability are the same assembly in two arrangements, so nothing here decides
/// which of the two rights is being exercised.
/// </remarks>
public interface IExports
{
    /// <summary>
    /// Assembles everything the library holds about the caller.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The export, or the refusal: <c>auth.stepup.required</c> where the session has
    /// not proved it is still the account, <c>auth.throttled</c> carrying
    /// <c>details.retryAt</c> where <c>privacy.export.ratelimit</c> is spent.
    /// </returns>
    ValueTask<Result<SubjectExport>> AssembleAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken);
}
