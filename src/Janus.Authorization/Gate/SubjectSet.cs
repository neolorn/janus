using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Who a principal is, for the purpose of reading grants: the account itself and every
/// group holding it at any depth.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-002 and AUTHZ-CACHE-001. The set is small and bounded, and is
/// resolved once per request rather than once per check. It is the principal's grant
/// rows and group set that are cached, never a resolved outcome.
/// </remarks>
internal sealed class SubjectSet
{
    private SubjectSet(Guid[] accounts, Guid[] groups, long version)
    {
        Accounts = accounts;
        Groups = groups;
        Version = version;
    }

    /// <summary>
    /// The account, or nothing where a system principal is asking.
    /// </summary>
    public Guid[] Accounts { get; }

    /// <summary>
    /// The groups holding the account, at any depth.
    /// </summary>
    public Guid[] Groups { get; }

    /// <summary>
    /// The counter the set was read at, which a later change raises.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// The set an account resolves to.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <param name="groups">The groups holding it, at any depth.</param>
    /// <param name="version">The counter the set was read at.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">The groups are absent.</exception>
    public static SubjectSet Of(SubjectId subject, IReadOnlyList<GroupId> groups, long version)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return new SubjectSet(
            [subject.Value],
            [.. groups.Select(group => group.Value)],
            version);
    }

    /// <summary>
    /// The set a principal with no account resolves to, which holds no grant at all.
    /// </summary>
    /// <returns>The empty set.</returns>
    public static SubjectSet None() => new([], [], 0);
}
