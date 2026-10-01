using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Configuration;

/// <summary>
/// The one way a runtime setting changes: classified for direction, gated where it
/// loosens, given a written reason, and written down.
/// </summary>
/// <param name="configuration">Where the settings are read.</param>
/// <param name="writes">Where the value in force is written, which nothing else reaches.</param>
/// <param name="audit">Where the change is written down.</param>
/// <param name="scope">Whether the caller may loosen the deployment.</param>
/// <param name="policies">Where what a change to the system policy raised is recorded.</param>
/// <param name="relay">Whether mail still reaches an Apple private relay address after a change.</param>
/// <param name="alerts">Where a change that weakens a step-up gate is told.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-CFG-002, OPS-CFG-005, OPS-CFG-008, INT-MAIL-011 AC2, OPS-ALERT-001 and the
/// <c>system:administer</c> row of chapter 10 section 2.1. Nothing else holds
/// <see cref="IConfigurationWrites"/>: a change that went round this would be a change
/// nobody was told of and nobody had to answer for.
/// </remarks>
internal sealed class ConfigurationAdministration(
    IConfigurationStore configuration,
    IConfigurationWrites writes,
    IConfigurationAudit audit,
    AdministrativeScope scope,
    PolicyResolution policies,
    RelayRegistration relay,
    IAlertChannels alerts,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// Puts a value in force for one setting.
    /// </summary>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <param name="setting">The setting, from <see cref="Settings"/>.</param>
    /// <param name="value">What it becomes.</param>
    /// <param name="reason">Why, which every change carries.</param>
    /// <param name="challenge">
    /// What the <c>config:loosen</c> gate answered, which a loosening has to have met
    /// and a tightening need not.
    /// </param>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>Whether the change was made, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">The setting, the challenge or the context is absent.</exception>
    public async ValueTask<Result> ChangeAsync<TValue>(
        Setting<TValue> setting,
        TValue value,
        string? reason,
        StepUpChallenge challenge,
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(context);

        // Background work changes no setting: a change answers for itself through the
        // person who made it, and a system principal is nobody to answer (OPS-CFG-005).
        if (context.Acting is not SubjectId actor)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        Error? failure = null;

        // OPS-CFG-002 AC6, X3: the direction is decided on the value in force under the
        // row's lock, so a concurrent change waits and cannot turn a tightening into a
        // loosening.
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        await writes.HoldAsync(setting.Key, cancellationToken).ConfigureAwait(false);

        TValue before = (await configuration
                .ReadAsync(setting, cancellationToken)
                .ConfigureAwait(false))
            .Match(one => one, error => Held<TValue>(error, ref failure));

        if (failure is not null)
        {
            return await EndedAsync(failure, cancellationToken).ConfigureAwait(false);
        }

        bool loosening = Loosens(setting, before, value);

        if (await RefusalAsync(setting.Key, loosening, reason, challenge, context, cancellationToken)
                .ConfigureAwait(false) is Error refused)
        {
            return await EndedAsync(refused, cancellationToken).ConfigureAwait(false);
        }

        _ = (await writes
                .WriteAsync(setting, value, cancellationToken)
                .ConfigureAwait(false))
            .Match(one => one, error => Held<TValue>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // AUTH-FACT-017: the system policy is the one setting whose change can raise a
        // requirement, and what it raised is what a sign-in that does not meet it is
        // held against; a change that raises nothing clears what stood before.
        if (before is Policy was && value is Policy becomes)
        {
            _ = await policies
                .RaisedAsync(null, was, becomes, time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);

            // D-083, OPS-ALERT-001: a system policy that asks less at a step-up gate is
            // told as it is made.
            if (PolicyStrictness.WeakenedGates(was, becomes) is { Count: > 0 } weakened
                && (await alerts
                        .RaiseAsync(StepUpWeakening.Of(setting.Key, scope: null, weakened, time.GetUtcNow()), cancellationToken)
                        .ConfigureAwait(false))
                    .Match(() => (Error?)null, error => error) is Error unalerted)
            {
                return Result.Failure(unalerted);
            }
        }

        // OPS-ALERT-006 AC5 (D-166, 329): turning the export step-up off is told as it is
        // made, in the change's transaction, and a change whose alert cannot be raised
        // is not made.
        if (setting.Key == Settings.ExfiltrationExportStepUpRequired.Key
            && before is true
            && value is false
            && (await alerts
                    .RaiseAsync(StepUpWeakening.Of(setting.Key, time.GetUtcNow()), cancellationToken)
                    .ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unannounced)
        {
            return Result.Failure(unannounced);
        }

        // INT-MAIL-011 AC2 (entry 269): a change to what decides whether mail reaches a
        // relay address is read with the value it made, inside its transaction.
        if (Relayed(setting.Key)
            && (await relay.CheckAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error unraised)
        {
            return Result.Failure(unraised);
        }

        await audit
            .ChangedAsync(
                new ConfigurationChange(
                    setting.Key,
                    setting.Write(before),
                    setting.Write(value),
                    loosening,
                    reason?.Trim(),
                    actor,
                    context.BreakGlassReason,
                    time.GetUtcNow(),
                    Principal: null),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <summary>
    /// Puts a value in force for one member of a key that exists once per organization
    /// or once per declared category, and writes the change down.
    /// </summary>
    /// <typeparam name="TValue">The type of the member's value.</typeparam>
    /// <param name="family">The family, from <see cref="Settings"/>.</param>
    /// <param name="parameter">The organization identifier or the declared category.</param>
    /// <param name="value">What the member becomes.</param>
    /// <param name="before">
    /// The value in force the member's route read under the row's lock and classified
    /// the change against, which the record carries as what the member was.
    /// </param>
    /// <param name="loosening">
    /// Whether the member's own route judged the change a loosening, on the value in
    /// force it read under <see cref="HoldAsync{TValue}(SettingFamily{TValue}, string, CancellationToken)"/>.
    /// </param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the change was
    /// made in the session it opened, or nothing.
    /// </param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>Success, or why the store refused the value.</returns>
    /// <remarks>
    /// What a change to a member costs is its family's own rule (a policy may not fall
    /// below the system's, AUTH-STEP-002a), so the member's route judges it before
    /// calling this, inside the unit of work it began and under the member's row lock
    /// (X3, OPS-CFG-002 AC6); what every change shares, the write and the record, is
    /// here, in that same transaction.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The family is absent.</exception>
    public async ValueTask<Result> ChangeMemberAsync<TValue>(
        SettingFamily<TValue> family,
        string parameter,
        TValue value,
        TValue before,
        bool loosening,
        string? reason,
        SubjectId actor,
        string? breakGlassReason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(family);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        await writes.HoldAsync(family.For(parameter), cancellationToken).ConfigureAwait(false);

        if ((await writes
                .WriteAsync(family, parameter, value, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error refused)
        {
            return Result.Failure(refused);
        }

        await audit
            .ChangedAsync(
                new ConfigurationChange(
                    family.For(parameter),
                    family.Write(before),
                    family.Write(value),
                    loosening,
                    reason,
                    actor,
                    breakGlassReason,
                    time.GetUtcNow(),
                    Principal: null),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <summary>
    /// Takes a setting's row under a lock held to the end of the caller's transaction,
    /// so what the caller reads of it next is the committed value and a concurrent
    /// change of it waits.
    /// </summary>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <param name="setting">The setting, from <see cref="Settings"/>.</param>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>The work of taking it.</returns>
    /// <remarks>
    /// Implements X3 of D-166 for runtime settings (178). The caller has begun its unit
    /// of work; a route that decides on the value of a setting it does not write, as a
    /// policy change decides on the system policy, holds that row too.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The setting is absent.</exception>
    public ValueTask HoldAsync<TValue>(Setting<TValue> setting, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setting);

        return writes.HoldAsync(setting.Key, cancellationToken);
    }

    /// <summary>
    /// Takes one member's row under a lock held to the end of the caller's transaction,
    /// before the member's route reads the value in force and classifies its change.
    /// </summary>
    /// <typeparam name="TValue">The type of the member's value.</typeparam>
    /// <param name="family">The family, from <see cref="Settings"/>.</param>
    /// <param name="parameter">The organization identifier or the declared category.</param>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>The work of taking it.</returns>
    /// <remarks>
    /// Implements X3 of D-166 for runtime settings (178) and OPS-CFG-002 AC6. The route
    /// has begun its unit of work, and <see cref="ChangeMemberAsync{TValue}"/> joins it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The family is absent.</exception>
    public ValueTask HoldAsync<TValue>(
        SettingFamily<TValue> family,
        string parameter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(family);

        return writes.HoldAsync(family.For(parameter), cancellationToken);
    }

    /// <summary>
    /// Whether a change would be allowed, without making it: what its direction costs
    /// is decided here and the change is not written.
    /// </summary>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <param name="setting">The setting.</param>
    /// <param name="value">What it would become.</param>
    /// <param name="reason">Why, which every change carries.</param>
    /// <param name="challenge">What the <c>config:loosen</c> gate answered.</param>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>Whether it would be allowed, or why it would be refused.</returns>
    /// <remarks>
    /// A caller that does something irreversible before the change reads this first,
    /// so that nothing is done for a change that is then refused.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The setting, the challenge or the context is absent.</exception>
    public async ValueTask<Result> AllowedAsync<TValue>(
        Setting<TValue> setting,
        TValue value,
        string? reason,
        StepUpChallenge challenge,
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(context);

        Error? failure = null;

        TValue before = (await configuration
                .ReadAsync(setting, cancellationToken)
                .ConfigureAwait(false))
            .Match(one => one, error => Held<TValue>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        return await RefusalAsync(setting.Key, Loosens(setting, before, value), reason, challenge, context, cancellationToken)
                .ConfigureAwait(false) is Error refused
            ? Result.Failure(refused)
            : Result.Success();
    }

    /// <summary>
    /// What a change to one member costs where the family's own direction decides it,
    /// by the rule a key that exists once is judged by: a reason for every change, and
    /// the permission to loosen and the <c>config:loosen</c> gate for a loosening.
    /// </summary>
    /// <typeparam name="TValue">The type of the member's value.</typeparam>
    /// <param name="family">The family, from <see cref="Settings"/>.</param>
    /// <param name="parameter">The declared category.</param>
    /// <param name="loosening">
    /// Whether the change loosens, judged on the value in force read under
    /// <see cref="HoldAsync{TValue}(SettingFamily{TValue}, string, CancellationToken)"/>.
    /// </param>
    /// <param name="reason">Why, which every change carries.</param>
    /// <param name="challenge">What the <c>config:loosen</c> gate answered.</param>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the check.</param>
    /// <returns>Nothing where the change is allowed, or why it is refused.</returns>
    /// <remarks>
    /// Implements OPS-CFG-002, OPS-CFG-008 AC2 and PRIV-RET-001 for
    /// <c>retention.&lt;category&gt;</c> (D-166, 180).
    /// </remarks>
    /// <exception cref="ArgumentNullException">The family, the challenge or the context is absent.</exception>
    public ValueTask<Error?> RefusalAsync<TValue>(
        SettingFamily<TValue> family,
        string parameter,
        bool loosening,
        string? reason,
        StepUpChallenge challenge,
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(context);

        return RefusalAsync(family.For(parameter), loosening, reason, challenge, context, cancellationToken);
    }

    // Chapter 10 section 4.1a classifies each field of the system policy on its own, as
    // it does an organization's, so a policy that only asks more is a tightening.
    private static bool Loosens<TValue>(Setting<TValue> setting, TValue before, TValue after) =>
        before is Policy was && after is Policy becomes
            ? PolicyStrictness.Loosens(was, becomes)
            : setting.Loosens(before, after);

    // A tightening costs a written reason and nothing else; a loosening, and any change
    // to a key with no direction, costs the permission to loosen, the gate and a written
    // reason (OPS-CFG-002 AC1 to AC3, chapter 10 section 2.1). The reason is asked of
    // every change to a runtime setting, a tightening of the named restriction set
    // included (OPS-CFG-008 AC2).
    private async ValueTask<Error?> RefusalAsync(
        ConfigurationKey key,
        bool loosening,
        string? reason,
        StepUpChallenge challenge,
        AccessContext context,
        CancellationToken cancellationToken)
    {
        if (!loosening)
        {
            return Unexplained(key, reason);
        }

        if (await scope.RefusedAsync(context, Permissions.SystemAdminister, cancellationToken)
                .ConfigureAwait(false) is Error withheld)
        {
            return withheld;
        }

        if (!StepUpRefusal.Met(challenge))
        {
            return StepUpRefusal.Of(challenge);
        }

        return Unexplained(key, reason);
    }

    /// <summary>
    /// Judges the reason a change of a key carries.
    /// </summary>
    /// <param name="key">The key the change writes.</param>
    /// <param name="reason">The reason it carries, as it came.</param>
    /// <returns>
    /// Nothing where the reason reads, or the refusal: <c>config.change.reasonrequired</c>
    /// naming the key where it is absent or blank, <c>api.request.malformed</c> naming
    /// <c>reason</c> where it is past 1024 characters.
    /// </returns>
    /// <remarks>
    /// API-CONV-002: a free-text field is 1 to 1024 characters after trimming. A blank
    /// reason is the refusal 10 names for a change; one past the limit is a request the
    /// boundary does not read.
    /// </remarks>
    internal static Error? Unexplained(ConfigurationKey key, string? reason) =>
        (reason?.Trim().Length ?? 0) switch
        {
            0 => Error.From(
                ErrorCodes.ConfigurationChangeReasonRequired,
                "key",
                JsonSerializer.SerializeToElement(key.ToString())),
            > 1024 => Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement("reason")),
            _ => null,
        };

    // X9: a refusal made under the row's lock has written nothing, so the unit of work
    // is ended before the refusal returns, which releases the row and leaves the scope
    // clean for the next operation.
    private async ValueTask<Result> EndedAsync(Error refusal, CancellationToken cancellationToken)
    {
        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Failure(refusal);
    }

    private static bool Relayed(ConfigurationKey key) =>
        key == Settings.PolicyDefault.Key
        || key == Settings.NotificationEmailSendingDomain.Key
        || key == Settings.NotificationEmailRelayRegistered.Key;

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
