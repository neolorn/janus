using System;
using Janus.Core;

namespace Janus.Hosting.Recovery;

/// <summary>
/// One email of the account, as the enrolment document names it for a replace.
/// </summary>
/// <param name="Id">Which identifier.</param>
/// <param name="Value">The address as the person entered it.</param>
/// <param name="Primary">Whether it is the primary email.</param>
/// <remarks>Implements AUTH-RECOV-002 and D-224.</remarks>
internal sealed record EnrolmentIdentifierView(string Id, string Value, bool Primary)
{
    /// <summary>
    /// Reads an identifier.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The identifier is absent.</exception>
    public static EnrolmentIdentifierView Of(IdentifierSummary identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        return new EnrolmentIdentifierView(identifier.Id.ToString(), identifier.Value, identifier.Primary);
    }
}
