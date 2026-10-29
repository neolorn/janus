using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Authentication.Records;

namespace Janus.Hosting.Records;

/// <summary>
/// The state of a collection session, as <c>GET /records/collection</c> answers it.
/// </summary>
/// <param name="Age">
/// <c>asked</c> while the session asks the age and holds no answer, <c>answered</c>
/// once it holds one, <c>none</c> where it does not ask.
/// </param>
/// <param name="ExpiresAt">When the session stops answering.</param>
/// <param name="Contacts">The contacts staged.</param>
/// <remarks>Implements REC-COL-001, chapter 09 section 7a.</remarks>
internal sealed record CollectionStateView(
    string Age,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<CollectedContactView> Contacts)
{
    /// <summary>
    /// Shows a session's state.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The state is absent.</exception>
    public static CollectionStateView Of(CollectionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new CollectionStateView(
            !state.AsksAge ? "none" : state.AgeAnswered ? "answered" : "asked",
            state.ExpiresAt,
            [.. state.Contacts.Select(CollectedContactView.Of)]);
    }
}
