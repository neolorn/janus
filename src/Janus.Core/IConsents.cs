using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The consent and objection records one subject holds, and the four things a
/// subject may do to them.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, PRIV-CONS-001, PRIV-CONS-002, PRIV-CONS-008,
/// PRIV-CONS-011, PRIV-RIGHT-001a and chapter 09 section 7. Granting and withdrawing
/// are the same shape and the same number of steps, and none of the four waits on a
/// human.
/// </remarks>
public interface IConsents
{
    /// <summary>
    /// Every consent record held for the subject, withdrawn and superseded ones
    /// included.
    /// </summary>
    /// <param name="context">Whose records.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records, oldest first.</returns>
    ValueTask<Result<IReadOnlyList<ConsentRecord>>> ReadAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a consent for one purpose, against the notice version current now, as
    /// a record of its own. Where the subject holds a live record the purpose admits,
    /// nothing is recorded or raised; a live record the purpose no longer admits is
    /// stamped superseded by the grant that replaces it.
    /// </summary>
    /// <param name="context">Whose consent.</param>
    /// <param name="purpose">Which purpose, and only one.</param>
    /// <param name="mechanism">
    /// Where it was given. A grant named <see cref="ConsentMechanism.Dashboard"/> over
    /// a consent the subject holds superseded and not withdrawn, or over a live one
    /// the purpose no longer admits, is recorded as
    /// <see cref="ConsentMechanism.Reconsent"/>; every other is recorded as named.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, a grant over a live record the purpose admits included, or the
    /// refusal: a purpose the declaration does not carry, or one whose basis is not
    /// consent.
    /// </returns>
    ValueTask<Result> GrantAsync(
        AccessContext context,
        string purpose,
        ConsentMechanism mechanism,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes a consent back, which ends the purpose and not only the activity.
    /// </summary>
    /// <param name="context">Whose consent.</param>
    /// <param name="purpose">Which purpose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal.</returns>
    ValueTask<Result> WithdrawAsync(
        AccessContext context,
        string purpose,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every objection record held for the subject, withdrawn ones included.
    /// </summary>
    /// <param name="context">Whose records.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records, oldest first.</returns>
    ValueTask<Result<IReadOnlyList<ObjectionRecord>>> ObjectionsAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records an objection to one purpose, which is always honoured, as a record of
    /// its own. Where the subject's objection to the purpose stands, nothing is
    /// recorded or raised.
    /// </summary>
    /// <param name="context">Whose objection.</param>
    /// <param name="purpose">Which purpose.</param>
    /// <param name="mechanism">Where it was recorded.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, an objection while one stands included, or
    /// <c>privacy.purpose.notobjectable</c> where the purpose's basis carries no right
    /// to object.
    /// </returns>
    ValueTask<Result> ObjectAsync(
        AccessContext context,
        string purpose,
        ConsentMechanism mechanism,
        CancellationToken cancellationToken);

    /// <summary>
    /// Withdraws an objection, which resumes processing for that purpose.
    /// </summary>
    /// <param name="context">Whose objection.</param>
    /// <param name="purpose">Which purpose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal.</returns>
    ValueTask<Result> WithdrawObjectionAsync(
        AccessContext context,
        string purpose,
        CancellationToken cancellationToken);
}
