using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// What the person setting a deployment up names on the command line for one of its
/// organizations.
/// </summary>
/// <param name="Name">The organization's name, as entered.</param>
/// <param name="Policy">
/// The policy it is held to, read from the file named as the policy object of the
/// policy route reads it, without the domain lock.
/// </param>
/// <param name="Reason">Why, which every change the command makes is recorded with.</param>
/// <param name="MultipleMemberships">
/// Whether one account may hold memberships of several organizations, where the command
/// line names it, or <see langword="null"/> where it leaves the setting as it stands.
/// </param>
/// <remarks>Implements OPS-BOOT-001, IDN-ORG-002 and D-248.</remarks>
internal sealed record OrganizationFromServerRequest(
    string Name,
    PolicyOverride Policy,
    string Reason,
    bool? MultipleMemberships);
