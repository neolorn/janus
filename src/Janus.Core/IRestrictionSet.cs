using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The named restriction set that governs every send: read and edited under
/// <c>restriction:edit</c>, and credit granted under one of them under
/// <c>restriction:grant</c>, each in the administrative organization.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTH-ABUSE-004, OPS-CFG-002 and chapter 09 section 8. Every
/// edit and every grant is a step-up action; a loosening also needs a reason and
/// raises a Normal alert, and a grant always needs a reason.
/// </remarks>
public interface IRestrictionSet
{
    /// <summary>
    /// Every restriction in force, the shipped defaults included.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The restrictions, or the refusal.</returns>
    ValueTask<Result<IReadOnlyList<Restriction>>> AllAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// One restriction by its name.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="name">Which restriction.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// The restriction, or the refusal: <c>api.request.malformed</c> naming <c>name</c>
    /// where no restriction has it.
    /// </returns>
    ValueTask<Result<Restriction>> ReadAsync(
        AccessContext context,
        string name,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates or replaces one restriction; the change applies to the next send.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="replacement">What the restriction becomes, under its own name.</param>
    /// <param name="reason">Why, which a loosening requires.</param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>Success, or the refusal.</returns>
    ValueTask<Result> EditAsync(
        AccessContext context,
        SessionId session,
        Restriction replacement,
        string? reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one restriction, which is a loosening whether or not it was shipped.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="name">Which restriction.</param>
    /// <param name="reason">Why, which the loosening requires.</param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>
    /// Success, or the refusal: <c>api.request.malformed</c> naming <c>name</c> where
    /// no restriction has it.
    /// </returns>
    ValueTask<Result> DeleteAsync(
        AccessContext context,
        SessionId session,
        string name,
        string? reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds credit to one key under one restriction: never a bypass, since the key is
    /// refused again once the credit is spent.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="name">Which restriction.</param>
    /// <param name="keyValue">The plain address, account, source or host value.</param>
    /// <param name="credit">How many sends the credit is worth.</param>
    /// <param name="reason">Why, which every grant requires.</param>
    /// <param name="cancellationToken">Abandons the grant.</param>
    /// <returns>Success, or the refusal.</returns>
    ValueTask<Result> GrantAsync(
        AccessContext context,
        SessionId session,
        string name,
        string keyValue,
        int credit,
        string? reason,
        CancellationToken cancellationToken);
}
