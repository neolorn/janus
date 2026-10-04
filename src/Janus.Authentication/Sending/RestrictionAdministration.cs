using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
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
/// <param name="scope">Whether the caller still holds the permission inside the unit of work.</param>
/// <param name="ledger">Where credit is added.</param>
/// <param name="audit">Where the change is written down.</param>
/// <param name="suppliers">The host-registered key suppliers.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="events">Where the emitted events go.</param>
/// <param name="alerts">Where a loosening's and a grant's alerts go.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004, OPS-CFG-002, OPS-CFG-008 and OPS-ALERT-001. A grant is
/// credit, never a bypass: the key it names is refused again once the credit is
/// spent, and the plain value of that key is never written down.
/// </remarks>
internal sealed class RestrictionAdministration(
    IConfigurationStore configuration,
    ConfigurationAdministration administration,
    AdministrativeScope scope,
    ISendLedger ledger,
    ISendAudit audit,
    RestrictionKeySuppliers suppliers,
    IUnitOfWork work,
    IEvents events,
    IAlertChannels alerts,
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
    /// <param name="reason">The written reason, which every edit requires.</param>
    /// <param name="challenge">What the <c>restriction:edit</c> gate answered.</param>
    /// <param name="context">Who is making the change.</param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>Whether the change was made, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">The name, the challenge or the context is absent.</exception>
    public async ValueTask<Result> EditAsync(
        string name,
        Restriction? replacement,
        string? reason,
        StepUpChallenge challenge,
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
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

        if (replacement is not null && Unsupplied(replacement) is Error unsupplied)
        {
            return Result.Failure(unsupplied);
        }

        // OPS-CFG-008: an edit of the set is a change to a runtime setting, and every
        // such change carries its reason whichever way it moves.
        if (Unexplained(reason, Error.From(
                ErrorCodes.ConfigurationChangeReasonRequired,
                "key",
                JsonSerializer.SerializeToElement(Settings.Restrictions.Key.ToString()))) is Error unexplained)
        {
            return Result.Failure(unexplained);
        }

        string stated = reason!.Trim();

        // INT-SMS-003: a set the key does not admit, a name outside the rule among it,
        // is refused before anything is begun, since the refusal writes nothing.
        if ((await ReplacedAsync(name, replacement, cancellationToken).ConfigureAwait(false))
            .Match(_ => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure(refused);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.RestrictionEdit, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: the set is read again under its row's lock and the edit made on what
        // is committed, so an edit of another restriction at the same moment is kept
        // rather than written over with the set as it stood before it.
        await administration.HoldAsync(Settings.Restrictions, cancellationToken).ConfigureAwait(false);

        Error? failure = null;

        Replaced replaced = (await ReplacedAsync(name, replacement, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Replaced>(error, ref failure));

        if (failure is Error unread)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unread);
        }

        Restriction? before = replaced.Before;
        bool loosening = Restrictions.IsLoosening(before, replacement);

        // OPS-CFG-002, OPS-CFG-005: every runtime write goes through the one operation
        // that classifies it, gates it and writes it down. The restriction set carries
        // its own direction, so a tightening passes here as it does at this method's
        // own gate.
        Result changed = await administration
            .ChangeAsync(
                Settings.Restrictions,
                replaced.Written,
                stated,
                challenge,
                context,
                cancellationToken)
            .ConfigureAwait(false);

        if (changed.Match(() => (Error?)null, error => error) is Error unchanged)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unchanged);
        }

        DateTimeOffset now = time.GetUtcNow();

        await audit
            .EditedAsync(
                name,
                before,
                replacement,
                loosening,
                stated,
                actor,
                context.BreakGlassReason,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new SendingRestrictionChanged(now, Edit + ":" + name + ":" + now.Ticks, name, loosening)
                {
                    Actor = actor,
                    Effective = context.Effective,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unpublished);
        }

        if (loosening)
        {
            Result alerted = await alerts
                .RaiseAsync(
                    Alerts.Of(AlertCondition.RestrictionLoosened, name, now, Named(name)),
                    cancellationToken)
                .ConfigureAwait(false);

            if (alerted.Match(() => (Error?)null, error => error) is Error unalerted)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure(unalerted);
            }
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

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
    /// <param name="context">Who is granting it.</param>
    /// <param name="cancellationToken">Abandons the grant.</param>
    /// <returns>Whether the credit was added, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">A value, the challenge or the context is absent.</exception>
    public async ValueTask<Result> GrantAsync(
        string name,
        string keyValue,
        int credit,
        string? reason,
        StepUpChallenge challenge,
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(keyValue);
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

        if (Unexplained(reason, Error.From(ErrorCodes.ConfigurationChangeReasonRequired)) is Error unexplained)
        {
            return Result.Failure(unexplained);
        }

        string stated = reason!.Trim();

        Error? failure = null;

        IReadOnlyList<Restriction> declared = (await configuration
                .ReadAsync(Settings.Restrictions, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<Restriction>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // X5, D-166: a path naming a restriction the set does not hold names no record.
        if (declared.FirstOrDefault(one => string.Equals(one.Name, name, StringComparison.Ordinal))
            is not Restriction granted)
        {
            return Result.Failure(Error.From(ErrorCodes.RestrictionNotFound));
        }

        if (credit <= 0)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.ConfigurationValueNotAllowed,
                    new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["key"] = JsonSerializer.SerializeToElement(Settings.Restrictions.Key.ToString()),
                        ["allowed"] = JsonSerializer.SerializeToElement("a credit above zero"),
                    }));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.RestrictionGrant, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        await ledger
            .GrantAsync(new RestrictionKey(name, granted.Key, keyValue), credit, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = time.GetUtcNow();

        await audit
            .GrantedAsync(name, credit, stated, actor, context.BreakGlassReason, now, cancellationToken)
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
                    stated)
                {
                    Actor = actor,
                    Effective = context.Effective,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unpublished);
        }

        Result alerted = await alerts
            .RaiseAsync(
                Alerts.Of(AlertCondition.RestrictionGranted, name, now, Named(name)),
                cancellationToken)
            .ConfigureAwait(false);

        if (alerted.Match(() => (Error?)null, error => error) is Error unalerted)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unalerted);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    private static Result<Replaced> Accepted(IReadOnlyList<Restriction> declared, string name, Restriction? replacement)
    {
        List<Restriction> written =
            [.. declared.Where(one => !string.Equals(one.Name, name, StringComparison.Ordinal))];

        if (replacement is not null)
        {
            written.Add(replacement with { Name = name });
        }

        return Settings.Restrictions.Accept(written).Match(
            _ => Result.Success(new Replaced(
                declared.FirstOrDefault(one => string.Equals(one.Name, name, StringComparison.Ordinal)),
                written)),
            Result.Failure<Replaced>);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // API-CONV-002, X4: a reason is 1 to 1024 characters after trimming. A blank one is
    // the refusal 10 names for a change without one; one past the limit is a request
    // the boundary does not read.
    private static Error? Unexplained(string? reason, Error blank) =>
        (reason?.Trim().Length ?? 0) switch
        {
            0 => blank,
            > 1024 => Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement("reason")),
            _ => null,
        };

    private static Dictionary<string, JsonElement> Named(string restriction) =>
        new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
        {
            ["restriction"] = JsonSerializer.SerializeToElement(restriction),
        };

    // The set in force with the one restriction replaced or deleted, and what that
    // restriction was, or why the set the edit would leave is not one the key admits.
    private async ValueTask<Result<Replaced>> ReplacedAsync(
        string name,
        Restriction? replacement,
        CancellationToken cancellationToken) =>
        (await configuration
            .ReadAsync(Settings.Restrictions, cancellationToken)
            .ConfigureAwait(false))
            .Match(
                declared => Accepted(declared, name, replacement),
                Result.Failure<Replaced>);

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

    // One edit of the set: the restriction it replaces, and the set it leaves.
    private sealed record Replaced(Restriction? Before, IReadOnlyList<Restriction> Written);
}
