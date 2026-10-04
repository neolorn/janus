using System;
using System.Collections.Generic;
using System.Linq;
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
/// <param name="work">The transaction the change and its event are written in.</param>
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
    IUnitOfWork work,
    TimeProvider time)
{
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
    /// <param name="context">Who is making the change.</param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>Whether the change was made, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">A value, the challenge or the context is absent.</exception>
    public async ValueTask<Result> ChangeAsync(
        SendKind channel,
        IReadOnlyList<string> replacement,
        string? reason,
        StepUpChallenge challenge,
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId actor)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (!StepUpRefusal.Met(challenge))
        {
            return Result.Failure(StepUpRefusal.Of(challenge));
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
            .AllowedAsync(setting, replacement, reason, challenge, context, cancellationToken)
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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // OPS-ALERT-004a AC7: the destinations told above are those the change replaces
        // only while that value is still in force, so it is read again under the row's
        // lock, and a change another one overtook writes nothing.
        await administration.HoldAsync(setting, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> standing = (await configuration
                .ReadAsync(setting, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(failure);
        }

        if (!standing.SequenceEqual(previous, StringComparer.Ordinal))
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(
                Error.From(
                    ErrorCodes.ConfigurationChangeSuperseded,
                    "key",
                    JsonSerializer.SerializeToElement(setting.Key.ToString())));
        }

        // OPS-CFG-005: the change is written through the one operation that classifies
        // it, gates it and writes it down, which is what the destination change was
        // missing. It joins this transaction, so the change and its event commit
        // together (CONV-DESIGN-002).
        Result changed = await administration
            .ChangeAsync(setting, replacement, reason, challenge, context, cancellationToken)
            .ConfigureAwait(false);

        if (changed.Match(() => (Error?)null, error => error) is Error unchanged)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unchanged);
        }

        Result published = await events
            .PublishAsync(raised with { Actor = actor, Effective = context.Effective }, cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unpublished);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
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
