using System;
using System.Globalization;
using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// The event a grant made or revoked is announced by, wherever the change is made.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-001, AUTHZ-DERIVE-005 and chapter 10 section 5b (D-218). A
/// holder that is an account is the event's subject; a group is no data subject. A grant
/// on the whole organization names the record as <c>GET /admin/grants</c> does
/// (chapter 09 section 8a).
/// </remarks>
internal static class GrantChanges
{
    private const string OrganizationWide = "organization";

    /// <summary>
    /// The event announcing that a grant was made or revoked.
    /// </summary>
    /// <param name="grant">The grant, as written or as revoked.</param>
    /// <param name="change">Which of the two happened.</param>
    /// <param name="at">When.</param>
    /// <returns>The event, naming no actor; the caller adds who acted, where a person did.</returns>
    /// <exception cref="ArgumentNullException">The grant is absent.</exception>
    public static GrantChanged Of(Grant grant, GrantChange change, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(grant);

        string kind = change is GrantChange.Made ? "grant-made" : "grant-revoked";

        return new GrantChanged(
            at,
            string.Create(CultureInfo.InvariantCulture, $"{kind}:{grant.Id.Value}@{at.UtcTicks}"),
            grant.Id,
            grant.Kind,
            grant.Subject.Type,
            grant.Subject.Value,
            grant.Role.ToString(),
            grant.Deny,
            grant.Organization,
            grant.ResourceType?.ToString() ?? OrganizationWide,
            grant.ResourceId?.ToString() ?? grant.Organization.ToString(),
            change)
        {
            Subject = grant.Subject.Type is SubjectType.User ? new SubjectId(grant.Subject.Value) : null,
        };
    }
}
