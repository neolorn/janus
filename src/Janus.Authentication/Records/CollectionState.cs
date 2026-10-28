using System;
using System.Collections.Generic;

namespace Janus.Authentication.Records;

/// <summary>
/// What a collection session holds, as its endpoints answer it.
/// </summary>
/// <param name="Id">The session, which the browser's pre-authentication session carries.</param>
/// <param name="AsksAge">Whether the session asks the age question.</param>
/// <param name="AgeAnswered">Whether it holds an answer to it.</param>
/// <param name="ExpiresAt">When it stops answering.</param>
/// <param name="Contacts">The contacts staged.</param>
/// <remarks>
/// Implements REC-COL-001. It carries no age answer and no date: the screen needs to
/// know only whether the question stands.
/// </remarks>
internal sealed record CollectionState(
    CollectionSessionId Id,
    bool AsksAge,
    bool AgeAnswered,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<CollectedContact> Contacts)
{
    /// <summary>
    /// What a session holds.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <returns>Its state.</returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public static CollectionState Of(CollectionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new CollectionState(session.Id, session.AsksAge, session.AgeAnswered, session.ExpiresAt, [.. session.Contacts]);
    }
}
