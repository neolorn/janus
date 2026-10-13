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
/// organization: scoping is resolved from the resource (AUTHZ-SCOPE-001). A request
/// made in a break-glass session carries the reason given at the credential's use, so
/// every record the action writes carries it with the acting identity (OPS-BOOT-002,
/// D-170); only the library's boundary makes such a context.
/// </remarks>
public sealed class AccessContext
{
    private AccessContext(
        SubjectId? acting,
        SubjectId? effective,
        SystemPrincipal? principal,
        string? breakGlassReason)
    {
        Acting = acting;
        Effective = effective;
        Principal = principal;
        BreakGlassReason = breakGlassReason;
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
    /// The reason given at the use of the break-glass credential, where the request was
    /// made in the session it opened, or nothing.
    /// </summary>
    public string? BreakGlassReason { get; }

    /// <summary>
    /// Whether background work is asking rather than a person.
    /// </summary>
    public bool IsSystem => Principal is not null;

    /// <summary>
    /// A person asking under their own identity.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <returns>The context.</returns>
    public static AccessContext Of(SubjectId subject) =>
        new(subject, subject, principal: null, breakGlassReason: null);

    /// <summary>
    /// A person asking under another identity, which is the seam impersonation would
    /// use and which no current path produces.
    /// </summary>
    /// <param name="acting">Whose credentials the request is made under.</param>
    /// <param name="effective">Whose identity the action is taken under.</param>
    /// <returns>The context.</returns>
    public static AccessContext Of(SubjectId acting, SubjectId effective) =>
        new(acting, effective, principal: null, breakGlassReason: null);

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

        return new AccessContext(acting: null, effective: null, principal, breakGlassReason: null);
    }

    /// <summary>
    /// A person asking in a break-glass session, under their own identity and with the
    /// reason given at the credential's use. The boundary makes it from the session and
    /// nothing else does.
    /// </summary>
    /// <param name="subject">The account the session belongs to.</param>
    /// <param name="breakGlassReason">The reason the session keeps.</param>
    /// <returns>The context.</returns>
    /// <exception cref="ArgumentNullException">The reason is absent.</exception>
    internal static AccessContext InBreakGlass(SubjectId subject, string breakGlassReason)
    {
        ArgumentNullException.ThrowIfNull(breakGlassReason);

        return new AccessContext(subject, subject, principal: null, breakGlassReason);
    }
}
