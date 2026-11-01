namespace Janus.Cli.Organizations;

/// <summary>
/// What the <c>organization</c> command is asked to set up, and why.
/// </summary>
/// <param name="Name">The organization's name, as the command line gave it.</param>
/// <param name="Policy">The path of the file holding its policy.</param>
/// <param name="Reason">Why, as the command line gave it.</param>
/// <param name="MultipleMemberships">
/// Whether one account may hold memberships of several organizations, where the command
/// line names it.
/// </param>
internal sealed record OrganizationRequest(string Name, string Policy, string Reason, bool? MultipleMemberships);
