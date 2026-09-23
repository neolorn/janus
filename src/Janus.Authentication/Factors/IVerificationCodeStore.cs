using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Factors;

/// <summary>
/// Where verification codes are held, which is not where credentials are.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-004 AC2 and CONV-DESIGN-003. The area declares the port and
/// the persistence lives in the storage project; a code that has stopped answering is
/// swept, not left.
/// </remarks>
internal interface IVerificationCodeStore
{
    /// <summary>
    /// The code outstanding against a holder.
    /// </summary>
    /// <param name="holder">What the code was issued against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The code, or nothing.</returns>
    ValueTask<VerificationCode?> FindAsync(byte[] holder, CancellationToken cancellationToken);

    /// <summary>
    /// Records a newly issued code, replacing whatever the holder had outstanding.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask AddAsync(VerificationCode code, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change to a code already held.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask RecordAsync(VerificationCode code, CancellationToken cancellationToken);

    /// <summary>
    /// Ends a code, whether it was spent or its tries ran out.
    /// </summary>
    /// <param name="holder">What the code was issued against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask RemoveAsync(byte[] holder, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the codes that have stopped answering.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were cleared.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
