using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Alerting;
using Janus.Privacy;

namespace Janus.Hosting.Configuration;

/// <summary>
/// Reading and changing one runtime key through the administration interface.
/// </summary>
/// <param name="scope">Whether the caller may administer the deployment.</param>
/// <param name="guard">What the session's proof amounts to against a gate.</param>
/// <param name="configuration">Where the values in force are read.</param>
/// <param name="administration">The one operation a runtime setting is written through.</param>
/// <param name="destinations">The one way the alert destinations change.</param>
/// <param name="declaration">The categories of data the host declared, with their floors.</param>
/// <param name="retention">How long a declared category is kept now.</param>
/// <param name="codec">
/// What the deployment reads uploaded images with, or nothing where it declared none.
/// </param>
/// <param name="work">The one transaction a change runs in.</param>
/// <remarks>
/// Implements LIB-API-005, OPS-CFG-002, OPS-CFG-003, OPS-CFG-004, OPS-CFG-005,
/// OPS-ALERT-004a, PRIV-RET-001 and chapter 09 section 8. The keys served are the
/// deployment's own and the retention of every category the host declared; the named
/// restriction set has its own operations and its own permission, and a key that
/// exists once per organization is changed where that organization is.
/// </remarks>
internal sealed class ConfigurationService(
    AdministrativeScope scope,
    StepUpGuard guard,
    IConfigurationStore configuration,
    ConfigurationAdministration administration,
    AlertDestinationChange destinations,
    AuthorizationDeclaration declaration,
    CategoryRetention retention,
    ImageCodec? codec,
    IUnitOfWork work) : IConfigurationAdministration
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

        if (Served.TryGetValue(key, out Setting? setting))
        {
            return await setting
                .Apply(new SettingReading(configuration, cancellationToken))
                .ConfigureAwait(false);
        }

        if (Declared(key) is not string category)
        {
            return Result.Failure<ConfiguredSetting>(Unserved());
        }

        // Chapter 09 section 8: a declared category reads with its floor as the
        // default, which is the period in force where the deployment stated none.
        return (await retention.ReadAsync(category, cancellationToken).ConfigureAwait(false)).Match(
            period => Result.Success(
                new ConfiguredSetting(
                    key,
                    Json(period),
                    Json(declaration.RetentionFloors[category]),
                    Protected: false,
                    Settings.HostCategoryRetention.Loosening)),
            Result.Failure<ConfiguredSetting>);
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
            return Declared(key) is string category
                ? await RetentionAsync(context, session, category, value, reason, cancellationToken)
                    .ConfigureAwait(false)
                : Result.Failure(Unserved());
        }

        // OPS-CFG-004: a protected key is not changeable through the application, by
        // anyone, whatever the value.
        if (setting.Scope is SettingScope.Protected)
        {
            return Result.Failure(Named(ErrorCodes.ConfigurationKeyProtected, key));
        }

        // OPS-CFG-005: a change answers for itself through the person who made it.
        if (context.Acting is not SubjectId actor)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (key == Settings.AlertingEmailDestinations.Key || key == Settings.AlertingSmsDestinations.Key)
        {
            return await DestinationsAsync(key, value, reason, context, actor, session, cancellationToken)
                .ConfigureAwait(false);
        }

        // IDN-ATTR-002, OPS-CFG-003: a system policy that shows photos needs the codec
        // the host declares, so it is refused while there is none.
        if (key == Settings.PolicyDefault.Key
            && codec is null
            && Settings.PolicyDefault.Received(value).Match(policy => policy.Photos, _ => false))
        {
            return Result.Failure(ProfilePhotos.Undeclared);
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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written. The one operation
        // that writes the setting joins this transaction.
        if (await scope.RefusedAsync(context, Permissions.ConfigurationManage, cancellationToken)
                .ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        Result changed = await setting
            .Apply(new SettingChange(administration, value, reason, challenge, context, cancellationToken))
            .ConfigureAwait(false);

        if (changed.Match<Error?>(() => null, error => error) is Error unchanged)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unchanged);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // OPS-ALERT-004a: the destination keys change through the one way that tells the
    // destinations being replaced, behind their own gate (chapter 10 section 5).
    private async ValueTask<Result> DestinationsAsync(
        ConfigurationKey key,
        JsonElement value,
        string reason,
        AccessContext context,
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
            .ChangeAsync(channel, replacement, reason, challenge, context, cancellationToken)
            .ConfigureAwait(false);
    }

    // PRIV-RET-001 and chapter 09 section 8: a declared category's retention changes
    // with the family's direction, where shortening loosens, never below the floor the
    // host declared. The direction is decided on the period in force under the
    // member's row lock (X3, OPS-CFG-002 AC6), and the one writer joins the same
    // transaction.
    private async ValueTask<Result> RetentionAsync(
        AccessContext context,
        SessionId session,
        string category,
        JsonElement value,
        string reason,
        CancellationToken cancellationToken)
    {
        SettingFamily<TimeSpan> family = Settings.HostCategoryRetention;
        ConfigurationKey key = family.For(category);

        // OPS-CFG-005: a change answers for itself through the person who made it.
        if (context.Acting is not SubjectId actor)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        Error? failure = null;

        // Chapter 09 section 8: a duration is a JSON string, and a value of another
        // JSON type is not allowed.
        TimeSpan period = (value.ValueKind is JsonValueKind.String
                ? family.Read(category, value.GetString()!)
                : Result.Failure<TimeSpan>(Named(ErrorCodes.ConfigurationValueNotAllowed, key)))
            .Match(one => one, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        TimeSpan floor = declaration.RetentionFloors[category];

        if (period < floor)
        {
            return Result.Failure(CategoryRetention.BelowFloor(key, floor));
        }

        StepUpChallenge challenge = (await guard
                .ChallengeAsync(actor, session, StepUpAction.ConfigLoosen, cancellationToken)
                .ConfigureAwait(false))
            .Match(one => one, error => Held<StepUpChallenge>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.ConfigurationManage, cancellationToken)
                .ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        await administration.HoldAsync(family, category, cancellationToken).ConfigureAwait(false);

        TimeSpan before = (await retention.ReadAsync(category, cancellationToken).ConfigureAwait(false))
            .Match(one => one, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(failure);
        }

        bool loosening = family.Loosens(before, period);

        if (await administration
                .RefusalAsync(family, category, loosening, reason, challenge, context, cancellationToken)
                .ConfigureAwait(false) is Error refused)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(refused);
        }

        if ((await administration
                .ChangeMemberAsync(
                    family,
                    category,
                    period,
                    before,
                    loosening,
                    reason.Trim(),
                    actor,
                    context.BreakGlassReason,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error unwritten)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unwritten);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // Chapter 09 section 8: the route serves retention.<category> for each category
    // the host declared, and no other member of the family.
    private string? Declared(ConfigurationKey key)
    {
        string prefix = Settings.HostCategoryRetention.Prefix + ".";
        string name = key.ToString();

        return name.StartsWith(prefix, StringComparison.Ordinal)
            && declaration.RetentionFloors.ContainsKey(name[prefix.Length..])
            ? name[prefix.Length..]
            : null;
    }

    private static JsonElement Json(TimeSpan period) =>
        JsonSerializer.SerializeToElement(Settings.HostCategoryRetention.Write(period));

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
