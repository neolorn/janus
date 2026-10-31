using Janus.Core.Configuration;

namespace Janus.Hosting.Configuration;

/// <summary>
/// Which profile attributes the screens ask for.
/// </summary>
/// <param name="LegalName">Whether the legal name is asked for (<c>profile.legalname</c>).</param>
/// <param name="DateOfBirth">Whether the date of birth is asked for (<c>profile.dateofbirth</c>).</param>
/// <remarks>Implements REG-PROF-001 and D-224.</remarks>
internal sealed record ProfileSettingsView(AttributeRequirement LegalName, AttributeRequirement DateOfBirth);
