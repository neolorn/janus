using System;
using Janus.Core;

namespace Janus.Hosting.Organizations;

/// <summary>
/// An issued invitation, as the response carries it.
/// </summary>
/// <param name="Id">The invitation.</param>
/// <param name="ExpiresAt">When its link stops opening anything.</param>
/// <param name="Token">
/// The token its link carries, where no email is bound for the link to go to; nothing
/// where it was sent.
/// </param>
/// <remarks>Implements chapter 09 section 8a, IDN-LIFE-009a and API-CONV-002.</remarks>
internal sealed record IssuedInvitationView(Guid Id, DateTimeOffset ExpiresAt, string? Token)
{
    /// <summary>
    /// The view of one issued invitation.
    /// </summary>
    /// <param name="issued">The invitation.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The invitation is absent.</exception>
    public static IssuedInvitationView Of(IssuedInvitation issued)
    {
        ArgumentNullException.ThrowIfNull(issued);

        return new(issued.Id.Value, issued.ExpiresAt, issued.Token);
    }
}
