using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Alerting;

/// <summary>
/// Changing where alerts go. The destinations being replaced are told first and the
/// change alert reaches them and not their replacements, so that redirecting the
/// alerting cannot be done quietly by whoever holds one stepped-up session.
/// </summary>
/// <param name="configuration">Where the destination lists are read.</param>
/// <param name="administration">The one operation a runtime setting is written through.</param>
/// <param name="router">What carries the alert to the previous destinations.</param>
/// <param name="events">Where the emitted events go.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-ALERT-004a and OPS-ALERT-004. The notice is not suppressible: no
/// setting reaches it, which is the hole the requirement closed.
/// </remarks>
internal sealed class AlertDestinationChange(
    IConfigurationStore configuration,
    ConfigurationAdministration administration,
    AlertRouter router,
    IEvents events,
    TimeProvider time)
{
    private const string Action = "alerting:destinations";

    /// <summary>
    /// Replaces the destination list of one channel.
    /// </summary>
    /// <param name="channel">Which channel.</param>
    /// <param name="replacement">The new list, which may not be empty.</param>
    /// <param name="reason">
    /// The written reason, which the change carries because the key has no direction
    /// and every change to one is classified as a loosening (OPS-CFG-002, D-079b).
    /// </param>
    /// <param name="challenge">What the <c>alerting:destinations</c> gate answered.</param>
    /// <param name="actor">Who is making the change.</param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>Whether the change was made, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">A value or the challenge is absent.</exception>
    public async ValueTask<Result> ChangeAsync(
        SendKind channel,
        IReadOnlyList<string> replacement,
        string? reason,
        StepUpChallenge challenge,
        SubjectId actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(challenge);

        if (!StepUpRefusal.Met(challenge))
        {
            return Result.Failure(StepUpRefusal.Of(Action, challenge));
        }

        TextListSetting setting = channel is SendKind.Email
            ? Settings.AlertingEmailDestinations
            : Settings.AlertingSmsDestinations;

        if (replacement.Count == 0)
        {
            // A channel with no destination is a channel that carries nothing, which
            // is the same blindness as turning the alerting off (OPS-ALERT-004a).
            return Result.Failure(
                Error.From(
                    ErrorCodes.ConfigurationLastDestination,
                    "key",
                    JsonSerializer.SerializeToElement(setting.Key.ToString())));
        }

        Error? failure = null;

        IReadOnlyList<string> previous = (await configuration
                .ReadAsync(setting, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // OPS-CFG-002, OPS-ALERT-004a: what the change costs is decided before the
        // notice goes out, because a notice of a change that was then refused tells
        // the destinations something that did not happen.
        Result allowed = await administration
            .AllowedAsync(setting, replacement, reason, challenge, AccessContext.Of(actor), cancellationToken)
            .ConfigureAwait(false);

        if (allowed.Match(() => (Error?)null, error => error) is Error disallowed)
        {
            return Result.Failure(disallowed);
        }

        DateTimeOffset now = time.GetUtcNow();

        AlertRaised raised = Alerts.Of(
            AlertCondition.AlertDestinationChanged,
            setting.Key.ToString(),
            now,
            Changed(setting, previous.Count, replacement.Count));

        // Before the change takes effect, and to the destinations it replaces.
        AlertAudience audience = channel is SendKind.Email
            ? new AlertAudience(previous, [])
            : new AlertAudience([], previous);

        _ = (await router.DeliverAsync(raised, audience, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<AlertDelivery>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // OPS-CFG-005: the change is written through the one operation that classifies
        // it, gates it and writes it down, which is what the destination change was
        // missing.
        Result changed = await administration
            .ChangeAsync(setting, replacement, reason, challenge, AccessContext.Of(actor), cancellationToken)
            .ConfigureAwait(false);

        if (changed.Match(() => (Error?)null, error => error) is Error unchanged)
        {
            return Result.Failure(unchanged);
        }

        Result published = await events
            .PublishAsync(raised with { Actor = actor }, cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure(unpublished);
        }

        return Result.Success();
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static Dictionary<string, JsonElement> Changed(
        Setting<IReadOnlyList<string>> setting,
        int before,
        int after) =>
        new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
        {
            ["key"] = JsonSerializer.SerializeToElement(setting.Key.ToString()),
            ["destinationsBefore"] = JsonSerializer.SerializeToElement(before),
            ["destinationsAfter"] = JsonSerializer.SerializeToElement(after),
        };
}
