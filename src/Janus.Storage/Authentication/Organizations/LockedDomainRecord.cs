using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Organizations;

/// <summary>
/// The <c>organization_domains</c> row: one domain an organization has listed in its
/// lock, with the token its TXT record carries.
/// </summary>
/// <remarks>
/// Implements REG-DOM-001 and IDN-ORG-006. A removal stamps the row and leaves it, so
/// the token stays drawn and the removal goes on refusing.
/// </remarks>
internal sealed class LockedDomainRecord
{
    /// <summary>The <c>token</c> column, which is this table's key.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>The <c>organization</c> column.</summary>
    public OrganizationId Organization { get; set; }

    /// <summary>The <c>domain</c> column, in its ASCII form.</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>The <c>added_at</c> column.</summary>
    public DateTimeOffset AddedAt { get; set; }

    /// <summary>The <c>verified_at</c> column, absent until the record is first found.</summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>The <c>checked_at</c> column, absent until the record is first looked for.</summary>
    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>The <c>last_check_passed</c> column.</summary>
    public bool? LastCheckPassed { get; set; }

    /// <summary>The <c>removed_at</c> column, absent while the lock lists the domain.</summary>
    public DateTimeOffset? RemovedAt { get; set; }
}
