using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Configuration;

/// <summary>
/// The one way a runtime setting changes: classified for direction, gated where it
/// loosens, given a written reason, and written down.
/// </summary>
/// <param name="configuration">Where the settings are read and written.</param>
/// <param name="audit">Where the change is written down.</param>
/// <param name="scope">Whether the caller may loosen the deployment.</param>
/// <param name="policies">Where what a change to the system policy raised is recorded.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-CFG-002, OPS-CFG-005, OPS-CFG-008 and the <c>system:administer</c>
/// row of chapter 10 section 2.1. Nothing else calls
/// <see cref="IConfigurationStore.WriteAsync{TValue}(Setting{TValue}, TValue, CancellationToken)"/>:
/// a change that went round this would be a change nobody was told of and nobody had
/// to answer for.
/// </remarks>
internal sealed class ConfigurationAdministration(
    IConfigurationStore configuration,
    IConfigurationAudit audit,
    AdministrativeScope scope,
    PolicyResolution policies,
    IUnitOfWork work,
    TimeProvider time)
{
    private const string Gate = "config:loosen";

    /// <summary>
    /// Puts a value in force for one setting.
    /// </summary>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <param name="setting">The setting, from <see cref="Settings"/>.</param>
    /// <param name="value">What it becomes.</param>
    /// <param name="reason">Why, which a loosening carries and a tightening need not.</param>
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

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        TValue before = (await configuration
                .ReadAsync(setting, cancellationToken)
                .ConfigureAwait(false))
            .Match(one => one, error => Held<TValue>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        bool loosening = setting.Loosens(before, value);

        if (await RefusalAsync(setting, loosening, reason, challenge, context, cancellationToken)
                .ConfigureAwait(false) is Error refused)
        {
            return Result.Failure(refused);
        }

        _ = (await configuration
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
        }

        await audit
            .ChangedAsync(
                new ConfigurationChange(
                    setting.Key,
                    setting.Write(before),
                    setting.Write(value),
                    loosening,
                    reason,
                    actor,
                    time.GetUtcNow()),
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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
    /// <param name="loosening">Whether the member's own route judged the change a loosening.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>What was in force before, or why the store refused the value.</returns>
    /// <remarks>
    /// What a change to a member costs is its family's own rule (a policy may not fall
    /// below the system's, AUTH-STEP-002a), so the member's route judges it before
    /// calling this; what every change shares, the write and the record, is here.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The family is absent.</exception>
    public async ValueTask<Result<TValue>> ChangeMemberAsync<TValue>(
        SettingFamily<TValue> family,
        string parameter,
        TValue value,
        bool loosening,
        string? reason,
        SubjectId actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(family);

        Error? failure = null;

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        TValue before = (await configuration
                .WriteAsync(family, parameter, value, cancellationToken)
                .ConfigureAwait(false))
            .Match(one => one, error => Held<TValue>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<TValue>(failure);
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
                    time.GetUtcNow()),
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(before);
    }

    /// <summary>
    /// Whether a change would be allowed, without making it: what its direction costs
    /// is decided here and the change is not written.
    /// </summary>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <param name="setting">The setting.</param>
    /// <param name="value">What it would become.</param>
    /// <param name="reason">Why, which a loosening carries.</param>
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

        return await RefusalAsync(setting, setting.Loosens(before, value), reason, challenge, context, cancellationToken)
                .ConfigureAwait(false) is Error refused
            ? Result.Failure(refused)
            : Result.Success();
    }

    // A tightening is free; a loosening, and any change to a key with no direction,
    // costs the permission to loosen, the gate and a written reason (OPS-CFG-002 AC1
    // to AC3, chapter 10 section 2.1).
    private async ValueTask<Error?> RefusalAsync<TValue>(
        Setting<TValue> setting,
        bool loosening,
        string? reason,
        StepUpChallenge challenge,
        AccessContext context,
        CancellationToken cancellationToken)
    {
        if (!loosening)
        {
            return null;
        }

        if (await scope.RefusedAsync(context, Permissions.SystemAdminister, cancellationToken)
                .ConfigureAwait(false) is Error withheld)
        {
            return withheld;
        }

        if (!StepUpRefusal.Met(challenge))
        {
            return StepUpRefusal.Of(Gate, challenge);
        }

        return string.IsNullOrWhiteSpace(reason)
            ? Named(ErrorCodes.RestrictionReasonRequired, setting)
            : null;
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static Error Named<TValue>(ErrorCode code, Setting<TValue> setting) =>
        Error.From(code, "key", JsonSerializer.SerializeToElement(setting.Key.ToString()));
}
