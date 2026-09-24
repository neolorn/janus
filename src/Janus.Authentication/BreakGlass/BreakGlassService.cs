using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.BreakGlass;

/// <summary>
/// The sealed emergency credential: generated from the management application or from
/// within a break-glass session, and presented once to open a time-boxed system
/// administration session for the reserved <c>emergency</c> account.
/// </summary>
/// <param name="store">Where the issues and the attempts are kept.</param>
/// <param name="emergency">Which account the session belongs to.</param>
/// <param name="audit">Where generation and use are written down.</param>
/// <param name="alerts">Where generation and use are raised.</param>
/// <param name="sessions">What opens the session.</param>
/// <param name="throttle">The progressive delay a source is held to.</param>
/// <param name="scope">Whether the caller administers the deployment.</param>
/// <param name="stepUp">What generation asks of the caller's session.</param>
/// <param name="hasher">What the code is hashed with, which is what a password is.</param>
/// <param name="configuration">Where the hashing parameters come from.</param>
/// <param name="addresses">Where the authentication application is.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where the code is drawn from.</param>
/// <remarks>
/// Implements OPS-BOOT-002, OPS-BOOT-004 and AUTH-STEP-004. Every attempt is counted
/// against the global limit before anything else is looked at, and in a transaction of
/// its own, so a refused attempt is counted as surely as one that succeeds. A group
/// whose check symbol does not hold is refused before any hash is compared.
/// </remarks>
internal sealed class BreakGlassService(
    IBreakGlassStore store,
    IEmergencyAccount emergency,
    IBreakGlassAudit audit,
    IAlertChannels alerts,
    SessionService sessions,
    ThrottleService throttle,
    AdministrativeScope scope,
    StepUpGuard stepUp,
    Argon2idHasher hasher,
    IConfigurationStore configuration,
    AuthenticationAddresses addresses,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    // OPS-BOOT-004: at most five attempts an hour, from all sources together.
    private const int GlobalAttempts = 5;

    private const string Generated = "generated";

    private const string Used = "used";

    // FE-BG-001: the authentication application's route, which the envelope names.
    private const string Page = "/break-glass";

    private static readonly TimeSpan GlobalWindow = TimeSpan.FromHours(1);

    private static readonly Factor[] Presented = [Factor.BreakGlass];

    /// <summary>
    /// Presents the credential, which spends it and opens the emergency session.
    /// </summary>
    /// <param name="credential">The code as it was typed or scanned.</param>
    /// <param name="origin">Where the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The session, or <c>auth.throttled</c> where the global limit or the source's delay
    /// holds, <c>auth.breakglass.consumed</c> where the code was already used, and
    /// <c>auth.breakglass.invalid</c> for any other code.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<IssuedSession>> PresentAsync(
        [NeverLogged] string credential,
        SessionOrigin origin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(origin);

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        int attempted = await store.AttemptedAsync(now, now - GlobalWindow, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        // The limit makes an attack loud rather than infeasible, so an hour from the
        // refused attempt is the earliest the answer promises (OPS-BOOT-004 AC7).
        if (attempted > GlobalAttempts)
        {
            return Result.Failure<IssuedSession>(Throttled(now + GlobalWindow));
        }

        var attempt = new ThrottleAttempt(origin.Address, Identifier: null);
        Error? failure = null;

        TimeSpan delay = (await throttle.DelayAsync(attempt, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedSession>(failure);
        }

        if (delay > TimeSpan.Zero)
        {
            return Result.Failure<IssuedSession>(Throttled(now + delay));
        }

        if (BreakGlassCode.Checked(credential) is not string canonical
            || await emergency.FindAsync(cancellationToken).ConfigureAwait(false) is not SubjectId account)
        {
            return await RefusedAsync(attempt, ErrorCodes.BreakGlassInvalid, cancellationToken).ConfigureAwait(false);
        }

        byte[] presented = BreakGlassCode.Presented(canonical);

        try
        {
            await work.BeginAsync(cancellationToken).ConfigureAwait(false);
            await store.HoldAsync(cancellationToken).ConfigureAwait(false);

            BreakGlassCredential? standing = await store.StandingAsync(cancellationToken).ConfigureAwait(false);

            if (standing is not null && Argon2idHasher.Verify(presented, standing.Hash))
            {
                return await UsedAsync(standing, account, origin, attempt, now, cancellationToken)
                    .ConfigureAwait(false);
            }

            bool spent = await store.LastConsumedAsync(cancellationToken).ConfigureAwait(false)
                is BreakGlassCredential last
                && Argon2idHasher.Verify(presented, last.Hash);

            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return await RefusedAsync(
                    attempt,
                    spent ? ErrorCodes.BreakGlassConsumed : ErrorCodes.BreakGlassInvalid,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(presented);
        }
    }

    /// <summary>
    /// Generates the credential, a first issue or a replacement that invalidates the
    /// one before it, and answers it the one time it is shown.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The code, or the refusal where the caller does not administer the deployment or
    /// the session has not stepped up.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<GeneratedBreakGlass>> GenerateAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure<GeneratedBreakGlass>(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.SystemAdminister, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<GeneratedBreakGlass>(refused);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.BreakGlassReplace, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure<GeneratedBreakGlass>(challenged);
        }

        Error? failure = null;

        Argon2StrengthClass parameters = (await ParametersAsync(cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Argon2StrengthClass>(error, ref failure));

        int parallelism = (await configuration
                .ReadAsync(Settings.PasswordArgon2Parallelism, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<GeneratedBreakGlass>(failure);
        }

        string code = BreakGlassCode.Draw(randomness);
        byte[] presented = BreakGlassCode.Presented(
            BreakGlassCode.Checked(code) ?? throw new InvalidOperationException("A drawn code holds every check."));
        PasswordHash hash;

        try
        {
            hash = hasher.Hash(presented, parameters, parallelism);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(presented);
        }

        DateTimeOffset now = time.GetUtcNow();
        var issued = BreakGlassCredential.Issue(hash, acting, now);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await store.HoldAsync(cancellationToken).ConfigureAwait(false);

        BreakGlassCredential? standing = await store.StandingAsync(cancellationToken).ConfigureAwait(false);

        if (standing is not null)
        {
            standing.Replace(now);

            _ = await store.RecordAsync(standing, cancellationToken).ConfigureAwait(false);
        }

        await store.AddAsync(issued, cancellationToken).ConfigureAwait(false);
        await audit.GeneratedAsync(acting, issued.Id, standing?.Id, now, cancellationToken).ConfigureAwait(false);

        // OPS-BOOT-004 AC2: generation reaches the owner as use does, whatever
        // alerting.owner.enabled says, and the one condition that does is the
        // break-glass row.
        Result alerted = await alerts
            .RaiseAsync(
                Alerts.Of(AlertCondition.BreakGlassUsed, Scope(Generated, issued.Id), now, Event(Generated)),
                cancellationToken)
            .ConfigureAwait(false);

        if (alerted.Match(() => (Error?)null, error => error) is Error unalerted)
        {
            return Result.Failure<GeneratedBreakGlass>(unalerted);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new GeneratedBreakGlass(
            code,
            new Uri(new Uri(addresses.Provider, UriKind.Absolute), Page),
            now));
    }

    private static Error Throttled(DateTimeOffset lifts) =>
        Error.From(ErrorCodes.Throttled, "retryAt", JsonSerializer.SerializeToElement(lifts));

    // OPS-ALERT-002: one issue is generated once and used once, and the two are two
    // alerts, so the use of an issue is never folded into the alert its generation
    // raised within the same window.
    private static string Scope(string happened, BreakGlassCredentialId issue) => happened + ":" + issue;

    private static Dictionary<string, JsonElement> Event(string happened) =>
        new(StringComparer.Ordinal)
        {
            ["event"] = JsonSerializer.SerializeToElement(happened),
        };

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<Result<IssuedSession>> UsedAsync(
        BreakGlassCredential standing,
        SubjectId account,
        SessionOrigin origin,
        ThrottleAttempt attempt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        standing.Consume(now);

        if (!await store.RecordAsync(standing, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<IssuedSession>(Error.From(ErrorCodes.BreakGlassConsumed));
        }

        return await (await sessions
                .BeginExemptAsync(account, Presented, origin, cancellationToken)
                .ConfigureAwait(false))
            .Match(
                issued => AnnouncedAsync(standing, account, issued, attempt, now, cancellationToken),
                unbegun => ValueTask.FromResult(Result.Failure<IssuedSession>(unbegun)))
            .ConfigureAwait(false);
    }

    private async ValueTask<Result<IssuedSession>> AnnouncedAsync(
        BreakGlassCredential standing,
        SubjectId account,
        IssuedSession issued,
        ThrottleAttempt attempt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await audit.UsedAsync(account, standing.Id, issued.Id, now, cancellationToken).ConfigureAwait(false);

        // OPS-BOOT-002 AC3: use is raised at once, on every channel, to the owner as
        // well as the operator.
        Result alerted = await alerts
            .RaiseAsync(
                Alerts.Of(AlertCondition.BreakGlassUsed, Scope(Used, standing.Id), now, Event(Used)),
                cancellationToken)
            .ConfigureAwait(false);

        if (alerted.Match(() => (Error?)null, error => error) is Error unalerted)
        {
            return Result.Failure<IssuedSession>(unalerted);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        await throttle.SucceededAsync(attempt, cancellationToken).ConfigureAwait(false);

        return Result.Success(issued);
    }

    private async ValueTask<Result<IssuedSession>> RefusedAsync(
        ThrottleAttempt attempt,
        ErrorCode refusal,
        CancellationToken cancellationToken)
    {
        Result counted = await throttle.FailedAsync(attempt, cancellationToken).ConfigureAwait(false);

        return counted.Match(
            () => Result.Failure<IssuedSession>(Error.From(refusal)),
            Result.Failure<IssuedSession>);
    }

    private async ValueTask<Result<Argon2StrengthClass>> ParametersAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        int memory = (await configuration.ReadAsync(Settings.PasswordArgon2Memory, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        int iterations = (await configuration.ReadAsync(Settings.PasswordArgon2Iterations, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        return failure is not null
            ? Result.Failure<Argon2StrengthClass>(failure)
            : Result.Success(new Argon2StrengthClass(memory, iterations));
    }
}
