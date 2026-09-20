using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Policies;

/// <summary>
/// The <c>policy_raises</c> row: one requirement a policy raised, and when.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-017. A raise belongs to the scope whose policy was changed:
/// the deployment's, where the organization is absent, or one organization's. Nothing
/// is written when a requirement is lowered, so a scope that has only ever loosened
/// holds no rows.
/// </remarks>
internal sealed class PolicyRaiseRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public Guid Id { get; set; }

    /// <summary>The <c>organization</c> column, absent for the deployment's own policy.</summary>
    public OrganizationId? Organization { get; set; }

    /// <summary>The <c>field</c> column: which of the two fields was raised.</summary>
    public PolicyField Field { get; set; }

    /// <summary>The <c>value</c> column: what the field now requires.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>The <c>raised_at</c> column, from which the run-up is counted.</summary>
    public DateTimeOffset RaisedAt { get; set; }
}
