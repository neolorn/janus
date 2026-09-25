using System;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// The grant just written, as the response carries it.
/// </summary>
/// <param name="Id">Its identifier, which a revocation names.</param>
/// <remarks>Implements chapter 09 section 8 and API-CONV-002.</remarks>
internal sealed record CreatedGrantView(Guid Id)
{
    /// <summary>
    /// The view of one grant's identifier.
    /// </summary>
    /// <param name="grant">The identifier.</param>
    /// <returns>The view.</returns>
    public static CreatedGrantView Of(GrantId grant) => new(grant.Value);
}
