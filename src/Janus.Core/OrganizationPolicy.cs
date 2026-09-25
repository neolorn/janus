namespace Janus.Core;

/// <summary>
/// The policy an organization's members resolve to, and what of it is the
/// organization's own.
/// </summary>
/// <param name="Resolved">
/// The system policy tightened by the organization's overrides.
/// </param>
/// <param name="Overridden">
/// The fields, and the gates by action, whose value in force is the organization's
/// own. A field it states that the system policy has since overtaken is absent, since
/// the value in force is then the system's.
/// </param>
/// <remarks>Implements chapter 09 section 8a, D-143 and AUTH-PRIN-002.</remarks>
public sealed record OrganizationPolicy(Policy Resolved, PolicyOverride Overridden);
