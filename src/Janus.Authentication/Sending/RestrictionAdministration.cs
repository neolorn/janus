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

namespace Janus.Authentication.Sending;

/// <summary>
/// Editing the named restrictions and granting credit under one of them: runtime
/// configuration, stepped up, audited, and alerted on where a change lets more
/// through than before.
/// </summary>
/// <param name="configuration">Where the restriction set is read.</param>
/// <param name="administration">The one operation a runtime setting is written through.</param>
/// <param name="ledger">Where credit is added.</param>
/// <param name="audit">Where the change is written down.</param>
/// <param name="suppliers">The host-registered key suppliers.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="events">Where the emitted events go.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004, OPS-CFG-002, OPS-CFG-008 and OPS-ALERT-001. A grant is
/// credit, never a bypass: the key it names is refused again once the credit is
/// spent, and the plain value of that key is never written down.
/// </remarks>
internal sealed class RestrictionAdministration(
    IConfigurationStore configuration,
    ConfigurationAdministration administration,
    ISendLedger ledger,
    ISendAudit audit,
    RestrictionKeySuppliers suppliers,
    IUnitOfWork work,
    IEvents events,
    TimeProvider time)
{
    private const string Edit = "restriction:edit";

    private const string Grant = "restriction:grant";

    /// <summary>
    /// Every restriction in force.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The restriction set, the shipped defaults included.</returns>
    public async ValueTask<Result<IReadOnlyList<Restriction>>> AllAsync(
        CancellationToken cancellationToken) =>
        await configuration.ReadAsync(Settings.Restrictions, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Creates, replaces or deletes one restriction. The change applies to the next
    /// send without a restart.
    /// </summary>
    /// <param name="name">Which restriction.</param>
    /// <param name="replacement">
    /// What it becomes, or nothing to delete it. Its name is the one given.
    /// </param>
    /// <param name="reason">The written reason, which a loosening requires.</param>
    /// <param name="challenge">What the <c>restriction:edit</c> gate answered.</param>
    /// <param name="actor">Who is making the change.</param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>Whether the change was made, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">The name or the challenge is absent.</exception>
    public async ValueTask<Result> EditAsync(
        string name,
        Restriction? replacement,
        string? reason,
        StepUpChallenge challenge,
        SubjectId actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(challenge);

        if (!StepUpRefusal.Met(challenge))
        {
            return Result.Failure(StepUpRefusal.Of(Edit, challenge));
        }

        Error? failure = null;

        IReadOnlyList<Restriction> declared = (await configuration
                .ReadAsync(Settings.Restrictions, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<Restriction>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        Restriction? before = declared.FirstOrDefault(one =>
            string.Equals(one.Name, name, StringComparison.Ordinal));

        if (replacement is not null && Unsupplied(replacement) is Error unsupplied)
        {
            return Result.Failure(unsupplied);
        }

        bool loosening = Restrictions.IsLoosening(before, replacement);

        if (loosening && string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.From(ErrorCodes.RestrictionReasonRequired));
        }

        List<Restriction> written =
            [.. declared.Where(one => !string.Equals(one.Name, name, StringComparison.Ordinal))];

        if (replacement is not null)
        {
            written.Add(replacement with { Name = name });
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        // OPS-CFG-002, OPS-CFG-005: every runtime write goes through the one operation
        // that classifies it, gates it and writes it down. The restriction set carries
        // its own direction, so a tightening passes here as it does at this method's
        // own gate.
        Result changed = await administration
            .ChangeAsync(
                Settings.Restrictions,
                written,
                reason,
                challenge,
                AccessContext.Of(actor),
                cancellationToken)
            .ConfigureAwait(false);

        if (changed.Match(() => (Error?)null, error => error) is Error unchanged)
        {
            return Result.Failure(unchanged);
        }

        DateTimeOffset now = time.GetUtcNow();

        await audit
            .EditedAsync(name, before, replacement, loosening, reason, actor, now, cancellationToken)
            .ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new SendingRestrictionChanged(now, Edit + ":" + name + ":" + now.Ticks, name, loosening)
                {
                    Actor = actor,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure(unpublished);
        }

        if (loosening)
        {
            Result alerted = await events
                .PublishAsync(
                    Alerts.Of(AlertCondition.RestrictionLoosened, name, now, Named(name)),
                    cancellationToken)
                .ConfigureAwait(false);

            if (alerted.Match(() => (Error?)null, error => error) is Error unalerted)
            {
                return Result.Failure(unalerted);
            }
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Adds credit to one key under one restriction, which support does for a person
    /// whose address a loop or an attacker has exhausted.
    /// </summary>
    /// <param name="name">Which restriction.</param>
    /// <param name="keyValue">The plain address, account, source or host value.</param>
    /// <param name="credit">How many sends the credit is worth.</param>
    /// <param name="reason">The written reason, which a grant requires.</param>
    /// <param name="challenge">What the <c>restriction:grant</c> gate answered.</param>
    /// <param name="actor">Who is granting it.</param>
    /// <param name="cancellationToken">Abandons the grant.</param>
    /// <returns>Whether the credit was added, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">A value or the challenge is absent.</exception>
    public async ValueTask<Result> GrantAsync(
        string name,
        string keyValue,
        int credit,
        string? reason,
        StepUpChallenge challenge,
        SubjectId actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(keyValue);
        ArgumentNullException.ThrowIfNull(challenge);

        if (!StepUpRefusal.Met(challenge))
        {
            return Result.Failure(StepUpRefusal.Of(Grant, challenge));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.From(ErrorCodes.RestrictionReasonRequired));
        }

        Error? failure = null;

        IReadOnlyList<Restriction> declared = (await configuration
                .ReadAsync(Settings.Restrictions, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<Restriction>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (credit <= 0
            || !declared.Any(one => string.Equals(one.Name, name, StringComparison.Ordinal)))
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.ConfigurationValueNotAllowed,
                    new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["key"] = JsonSerializer.SerializeToElement(Settings.Restrictions.Key.ToString()),
                        ["allowed"] = JsonSerializer.SerializeToElement(
                            "a declared restriction and a credit above zero"),
                    }));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        await ledger
            .GrantAsync(new RestrictionKey(name, keyValue), credit, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = time.GetUtcNow();

        await audit
            .GrantedAsync(name, credit, reason, actor, now, cancellationToken)
            .ConfigureAwait(false);

        // The plain key value never leaves this method: the event carries the
        // restriction, the credit and the reason (AUTH-ABUSE-004, chapter 10 5b).
        Result published = await events
            .PublishAsync(
                new SendingRestrictionGranted(
                    now,
                    Grant + ":" + name + ":" + now.Ticks,
                    name,
                    credit,
                    reason)
                {
                    Actor = actor,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure(unpublished);
        }

        Result alerted = await events
            .PublishAsync(
                Alerts.Of(AlertCondition.RestrictionGranted, name, now, Named(name)),
                cancellationToken)
            .ConfigureAwait(false);

        if (alerted.Match(() => (Error?)null, error => error) is Error unalerted)
        {
            return Result.Failure(unalerted);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static Dictionary<string, JsonElement> Named(string restriction) =>
        new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
        {
            ["restriction"] = JsonSerializer.SerializeToElement(restriction),
        };

    // Chapter 09 section 8: a host key no supplier answers for is a value the set does
    // not admit, refused where it is edited (422) rather than as the startup fault the
    // same absence is when a deployment declares it (LIB-HOST-001).
    private Error? Unsupplied(Restriction replacement) =>
        replacement.Key is RestrictionKeyKind.Host
        && (replacement.HostKeyName is null || !suppliers.TryFind(replacement.HostKeyName, out _))
            ? new Error(
                ErrorCodes.ConfigurationValueNotAllowed,
                new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                {
                    ["key"] = JsonSerializer.SerializeToElement(Settings.Restrictions.Key.ToString()),
                    ["supplier"] = JsonSerializer.SerializeToElement(replacement.HostKeyName ?? string.Empty),
                })
            : null;
}
