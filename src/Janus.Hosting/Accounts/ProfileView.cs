using System;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The profile, as the account application shows it.
/// </summary>
/// <param name="DisplayName">What the person is called.</param>
/// <param name="LegalName">The legal name, where the host collects one.</param>
/// <param name="DateOfBirth">The date of birth, where the host collects one.</param>
/// <param name="Photo">When the photo last changed, which is what the image is fetched by.</param>
/// <remarks>Implements REG-PROF-001 and IDN-ATTR-007.</remarks>
internal sealed record ProfileView(
    string? DisplayName,
    string? LegalName,
    DateOnly? DateOfBirth,
    DateTimeOffset? Photo)
{
    /// <summary>
    /// Reads the profile.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The profile is absent.</exception>
    public static ProfileView Of(ProfileDetail profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ProfileView(
            profile.DisplayName,
            profile.LegalName,
            profile.DateOfBirth,
            profile.PhotoUpdatedAt);
    }
}
