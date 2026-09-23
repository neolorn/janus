using System;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// The grant that decided an explanation.
/// </summary>
/// <param name="Id">The grant's identifier, or nothing for one a fact produced.</param>
/// <param name="Kind">Whether it is stored, derived or materialised.</param>
/// <param name="SubjectType">Whether it names a user or a group.</param>
/// <param name="SubjectId">The user or group it names.</param>
/// <param name="Role">The role it confers.</param>
/// <param name="Deny">Whether it denies.</param>
/// <param name="InheritedFrom">The container it sits on, or nothing where it sits on the record.</param>
/// <remarks>Implements AUTHZ-GATE-004 in the shape D-153 fixes.</remarks>
internal sealed record ExplainedGrantView(
    Guid? Id,
    GrantKind Kind,
    SubjectType SubjectType,
    Guid SubjectId,
    string Role,
    bool Deny,
    ResourceView? InheritedFrom)
{
    /// <summary>
    /// The view of a grant.
    /// </summary>
    /// <param name="grant">The grant.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The grant is absent.</exception>
    public static ExplainedGrantView Of(ExplainedGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return new ExplainedGrantView(
            grant.Id?.Value,
            grant.Kind,
            grant.SubjectType,
            grant.SubjectId,
            grant.Role.ToString(),
            grant.Deny,
            grant.InheritedFrom is ResourceReference container ? ResourceView.Of(container) : null);
    }
}
