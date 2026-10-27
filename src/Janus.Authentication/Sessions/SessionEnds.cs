using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// The events the sessions an end took are announced by, wherever the end is made.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-010 and chapter 10 section 5b (D-218). Each session is announced
/// once, naming its account as the subject and the person who ended it, where a person
/// did; a session past its expiry was ended by time and is not among them.
/// </remarks>
internal static class SessionEnds
{
    /// <summary>
    /// The event announcing that a session ended.
    /// </summary>
    /// <param name="ended">The session and its account.</param>
    /// <param name="reason">Why it ended.</param>
    /// <param name="at">When.</param>
    /// <returns>The event, naming no actor.</returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public static SessionEnded Of(EndedSession ended, SessionEndReason reason, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(ended);

        return new SessionEnded(
            at,
            string.Create(CultureInfo.InvariantCulture, $"session-ended:{ended.Session.Value}@{at.UtcTicks}"),
            ended.Session,
            reason)
        {
            Subject = ended.Subject,
        };
    }

    /// <summary>
    /// Announces each session an end took, in the end's transaction.
    /// </summary>
    /// <param name="events">Where the events go.</param>
    /// <param name="ended">The sessions.</param>
    /// <param name="reason">Why they ended.</param>
    /// <param name="at">When.</param>
    /// <param name="actor">The person who ended them, or nothing where no person did.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the failure where an event was not taken.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async ValueTask<Error?> AnnouncedAsync(
        IEvents events,
        IReadOnlyList<EndedSession> ended,
        SessionEndReason reason,
        DateTimeOffset at,
        AccessContext? actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(ended);

        foreach (EndedSession each in ended)
        {
            if ((await events
                    .PublishAsync(
                        Of(each, reason, at) with { Actor = actor?.Acting, Effective = actor?.Effective },
                        cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error unannounced)
            {
                return unannounced;
            }
        }

        return null;
    }
}
