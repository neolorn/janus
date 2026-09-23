using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Alerting;

namespace Janus.Hosting.Configuration;

/// <summary>
/// Reading and changing one runtime key through the administration interface.
/// </summary>
/// <param name="scope">Whether the caller may administer the deployment.</param>
/// <param name="guard">What the session's proof amounts to against a gate.</param>
/// <param name="configuration">Where the values in force are read.</param>
/// <param name="administration">The one operation a runtime setting is written through.</param>
/// <param name="destinations">The one way the alert destinations change.</param>
/// <remarks>
/// Implements LIB-API-005, OPS-CFG-002, OPS-CFG-003, OPS-CFG-004, OPS-CFG-005,
/// OPS-ALERT-004a and chapter 09 section 8. The keys served are the deployment's own;
/// the named restriction set has its own operations and its own permission, and a key
/// that exists once per organization or per declared category is changed where that
/// organization or category is.
/// </remarks>
internal sealed class ConfigurationService(
    AdministrativeScope scope,
    StepUpGuard guard,
    IConfigurationStore configuration,
    ConfigurationAdministration administration,
    AlertDestinationChange destinations) : IConfigurationAdministration
{
    private static readonly FrozenDictionary<ConfigurationKey, Setting> Served = Settings.All
        .Where(setting => setting.Key != Settings.Restrictions.Key)
        .ToFrozenDictionary(setting => setting.Key);

    /// <inheritdoc/>
    public async ValueTask<Result<ConfiguredSetting>> ReadAsync(
        AccessContext context,
        ConfigurationKey key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope.RefusedAsync(context, Permissions.ConfigurationRead, cancellationToken)
                .ConfigureAwait(false) is Error refused)
        {
            return Result.Failure<ConfiguredSetting>(refused);
        }

        if (!Served.TryGetValue(key, out Setting? setting))
        {
            return Result.Failure<ConfiguredSetting>(Unserved());
        }

        return await setting
            .Apply(new SettingReading(configuration, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> ChangeAsync(
        AccessContext context,
        SessionId session,
        ConfigurationKey key,
        JsonElement value,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reason);

        if (await scope.RefusedAsync(context, Permissions.ConfigurationManage, cancellationToken)
                .ConfigureAwait(false) is Error refused)
        {
            return Result.Failure(refused);
        }

        if (!Served.TryGetValue(key, out Setting? setting))
        {
            return Result.Failure(Unserved());
        }

        // OPS-CFG-004: a protected key is not changeable through the application, by
        // anyone, whatever the value.
        if (setting.Scope is SettingScope.Protected)
        {
            return Result.Failure(Named(ErrorCodes.ConfigurationKeyProtected, key));
        }

        // Chapter 09 section 8: the reason is required on every change, the tightening
        // included, and is what the audit entry answers with.
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Named(ErrorCodes.RestrictionReasonRequired, key));
        }

        // OPS-CFG-005: a change answers for itself through the person who made it.
        if (context.Acting is not SubjectId actor)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (key == Settings.AlertingEmailDestinations.Key || key == Settings.AlertingSmsDestinations.Key)
        {
            return await DestinationsAsync(key, value, reason, actor, session, cancellationToken)
                .ConfigureAwait(false);
        }

        Error? failure = null;

        StepUpChallenge challenge = (await guard
                .ChallengeAsync(actor, session, StepUpAction.ConfigLoosen, cancellationToken)
                .ConfigureAwait(false))
            .Match(one => one, error => Held<StepUpChallenge>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        return await setting
            .Apply(new SettingChange(administration, value, reason, challenge, context, cancellationToken))
            .ConfigureAwait(false);
    }

    // OPS-ALERT-004a: the destination keys change through the one way that tells the
    // destinations being replaced, behind their own gate (chapter 10 section 5).
    private async ValueTask<Result> DestinationsAsync(
        ConfigurationKey key,
        JsonElement value,
        string reason,
        SubjectId actor,
        SessionId session,
        CancellationToken cancellationToken)
    {
        (TextListSetting setting, SendKind channel) = key == Settings.AlertingEmailDestinations.Key
            ? (Settings.AlertingEmailDestinations, SendKind.Email)
            : (Settings.AlertingSmsDestinations, SendKind.Sms);

        Error? failure = null;

        IReadOnlyList<string> replacement = setting.Received(value)
            .Match(one => one, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        StepUpChallenge challenge = (await guard
                .ChallengeAsync(actor, session, StepUpAction.AlertingDestinations, cancellationToken)
                .ConfigureAwait(false))
            .Match(one => one, error => Held<StepUpChallenge>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        return await destinations
            .ChangeAsync(channel, replacement, reason, challenge, actor, cancellationToken)
            .ConfigureAwait(false);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static Error Named(ErrorCode code, ConfigurationKey key) =>
        Error.From(code, "key", JsonSerializer.SerializeToElement(key.ToString()));

    private static Error Unserved() =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement("key"));
}
