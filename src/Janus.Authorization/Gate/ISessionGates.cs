using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where the gate asks whether the session a person acts on has proved what a named
/// step-up gate costs under that person's policy.
/// </summary>
/// <remarks>
/// Implements AUTH-STEP-001, AUTH-STEP-002, AUTHZ-GATE-005 and CONV-LAYOUT-001. The
/// session record and the policy are authentication's, so the gate reaches them through
/// this narrow view and never reads a factor itself.
/// </remarks>
internal interface ISessionGates
{
    /// <summary>
    /// Whether a session of the library carries the request the context acts in, so
    /// that a gate can be judged against what it proved.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <returns>Whether the acting person's own live session carries the request.</returns>
    bool Judges(AccessContext context);

    /// <summary>
    /// What the named gate still asks of the acting person's session.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="gate">The gate's name: one of chapter 10 section 5a, or one the host names.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where the session meets the gate, or the refusal naming what the gate
    /// costs and what the person can present.
    /// </returns>
    ValueTask<Error?> OutstandingAsync(AccessContext context, string gate, CancellationToken cancellationToken);

    /// <summary>
    /// What a capability bound to the named gate still requires of the acting person's
    /// session, as chapter 10 section 5.20 names it.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="gate">The gate's name: one of chapter 10 section 5a, or one the host names.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where the session meets the gate; <c>reauthenticate</c> where it would but
    /// for proof last reached before its last downgrade (AUTH-SESS-009); otherwise
    /// <c>stepup</c>.
    /// </returns>
    ValueTask<CapabilityResidual?> ResidualAsync(AccessContext context, string gate, CancellationToken cancellationToken);

    /// <summary>
    /// What the named gate costs under the acting person's policy, where no session of
    /// the library carries the request and a host reports what the caller proved.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="gate">The gate's name: one of chapter 10 section 5a, or one the host names.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The gate's three values: a section 5a gate as the policy states it, and one the
    /// host names at the dearest gate of that policy; or the failure where no person
    /// acts or the policy cannot be read.
    /// </returns>
    ValueTask<Result<Core.Gate>> CostAsync(AccessContext context, string gate, CancellationToken cancellationToken);
}
