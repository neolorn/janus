using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sessions;

/// <summary>
/// The watch over one account's sessions used from implausibly distant places inside
/// one window.
/// </summary>
/// <param name="sessions">Where the account's other sessions are read.</param>
/// <param name="configuration">Where the window and the distance are read.</param>
/// <param name="alerts">Where the condition is raised.</param>
/// <remarks>
/// <para>
/// Implements OPS-ALERT-007. Two sessions are implausible when both were used inside
/// <c>alerting.sessions.window</c> and their resolved cities lie further apart than
/// <c>alerting.sessions.distance</c> or in different countries. The same city, or a
/// city unresolved on either side, never raises anything. Sessions standing on one
/// record are one session held more than one way and are never compared.
/// </para>
/// <para>
/// A use is looked at when it begins a stretch: a session begun, a use from a city
/// other than the one it was last used from, or a use after a pause longer than the
/// window. Every session used inside a stretch was looked at when the later of the two
/// began its own, so a stretch that goes on in one city asks nothing more.
/// </para>
/// </remarks>
internal sealed class ConcurrentSessions(
    ISessionStore sessions,
    IConfigurationStore configuration,
    IAlertChannels alerts)
{
    /// <summary>
    /// Looks at one use of a session against the account's other sessions.
    /// </summary>
    /// <param name="session">The session, already recording this use.</param>
    /// <param name="before">Where it was last used from before this use, or nothing where it has just begun.</param>
    /// <param name="usedBefore">When it was last used before this use, or nothing where it has just begun.</param>
    /// <param name="now">When this use was.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the failure where the condition could not be raised.</returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public async ValueTask<Result> WatchAsync(
        Session session,
        SessionOrigin? before,
        DateTimeOffset? usedBefore,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!Resolved(session.LastSeen))
        {
            return Result.Success();
        }

        TimeSpan window = (await configuration
                .ReadAsync(Settings.AlertingSessionsWindow, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.AlertingSessionsWindow.Default);

        if (before is not null
            && usedBefore is DateTimeOffset at
            && at >= now - window
            && SameCity(before, session.LastSeen))
        {
            return Result.Success();
        }

        int distance = (await configuration
                .ReadAsync(Settings.AlertingSessionsDistance, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.AlertingSessionsDistance.Default);

        foreach (Session other in await sessions
                     .LiveOfAsync(session.Subject, now, cancellationToken)
                     .ConfigureAwait(false))
        {
            if (other.Spine == session.Spine
                || other.LastSeenAt < now - window
                || !Implausible(session.LastSeen, other.LastSeen, distance))
            {
                continue;
            }

            return await alerts
                .RaiseAsync(
                    Alerts.Of(
                        AlertCondition.ConcurrentSessionsImplausible,
                        session.Subject.ToString(),
                        now,
                        Apart(session, other)),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success();
    }

    private static bool Resolved(SessionOrigin origin) =>
        origin.Location?.City is not null && origin.Coordinates is not null;

    private static bool SameCity(SessionOrigin one, SessionOrigin other) =>
        string.Equals(one.Location?.City, other.Location?.City, StringComparison.OrdinalIgnoreCase)
        && string.Equals(one.Location?.Country, other.Location?.Country, StringComparison.OrdinalIgnoreCase);

    private static bool Implausible(SessionOrigin one, SessionOrigin other, int distance)
    {
        if (!Resolved(other) || SameCity(one, other))
        {
            return false;
        }

        if (one.Location?.Country is string country
            && other.Location?.Country is string otherCountry
            && !string.Equals(country, otherCountry, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return one.Coordinates!.Value.KilometresTo(other.Coordinates!.Value) > distance;
    }

    // What the operator needs to find the two sessions, and nothing of where either
    // was: the places are the person's and stay under their key.
    private static Dictionary<string, JsonElement> Apart(Session session, Session other) =>
        new(capacity: 3, StringComparer.Ordinal)
        {
            ["subject"] = JsonSerializer.SerializeToElement(session.Subject.ToString()),
            ["session"] = JsonSerializer.SerializeToElement(session.Id.ToString()),
            ["other"] = JsonSerializer.SerializeToElement(other.Id.ToString()),
        };
}
