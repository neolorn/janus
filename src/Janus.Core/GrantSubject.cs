using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// Who a grant is held by: one account, or a group whose members hold it
/// transitively. One table serves both, because both are the same sentence with a
/// different noun.
/// </summary>
/// <param name="Type">Whether it is an account or a group.</param>
/// <param name="Value">The account's or the group's identifier.</param>
/// <remarks>Implements AUTHZ-GRANT-001, chapter 10 section 5.5, CONV-DESIGN-004.</remarks>
public readonly record struct GrantSubject(SubjectType Type, Guid Value)
{
    /// <summary>
    /// The grant is held by one account.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <returns>The holder.</returns>
    public static GrantSubject Of(SubjectId subject) => new(SubjectType.User, subject.Value);

    /// <summary>
    /// The grant is held by a group, and so by every member of it at any depth.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The holder.</returns>
    public static GrantSubject Of(GroupId group) => new(SubjectType.Group, group.Value);

    /// <inheritdoc/>
    public override string ToString() =>
        (Type == SubjectType.Group ? "group:" : "user:")
        + Value.ToString("D", CultureInfo.InvariantCulture);
}
