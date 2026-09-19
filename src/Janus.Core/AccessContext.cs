using System;

namespace Janus.Core;

/// <summary>
/// Who is asking. Every operation takes one, and calling a service in process is no
/// way round the permission an endpoint would apply.
/// </summary>
/// <remarks>
/// Implements AUTHZ-IMP-001, IDN-PRIN-001 and LIB-API-005. The acting and the
/// effective identity are two fields where one would do, so that "who did this" is
/// never inferred; no current path reads them as differing. The context names no
/// organization: scoping is resolved from the resource (AUTHZ-SCOPE-001).
/// </remarks>
public sealed class AccessContext
{
    private AccessContext(SubjectId? acting, SubjectId? effective, SystemPrincipal? principal)
    {
        Acting = acting;
        Effective = effective;
        Principal = principal;
    }

    /// <summary>
    /// Whose credentials the request is made under, or nothing where a system
    /// principal is acting.
    /// </summary>
    public SubjectId? Acting { get; }

    /// <summary>
    /// Whose identity the action is taken under, or nothing where a system principal
    /// is acting.
    /// </summary>
    public SubjectId? Effective { get; }

    /// <summary>
    /// The named principal doing background work, or nothing where a person is asking.
    /// </summary>
    public SystemPrincipal? Principal { get; }

    /// <summary>
    /// Whether background work is asking rather than a person.
    /// </summary>
    public bool IsSystem => Principal is not null;

    /// <summary>
    /// A person asking under their own identity.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <returns>The context.</returns>
    public static AccessContext Of(SubjectId subject) => new(subject, subject, principal: null);

    /// <summary>
    /// A person asking under another identity, which is the seam impersonation would
    /// use and which no current path produces.
    /// </summary>
    /// <param name="acting">Whose credentials the request is made under.</param>
    /// <param name="effective">Whose identity the action is taken under.</param>
    /// <returns>The context.</returns>
    public static AccessContext Of(SubjectId acting, SubjectId effective) =>
        new(acting, effective, principal: null);

    /// <summary>
    /// Background work asking as a named principal with a stated reason.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The context.</returns>
    /// <exception cref="ArgumentNullException">
    /// The principal is absent, which is the null principal IDN-PRIN-001 rejects.
    /// </exception>
    public static AccessContext Of(SystemPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return new AccessContext(acting: null, effective: null, principal);
    }
}
