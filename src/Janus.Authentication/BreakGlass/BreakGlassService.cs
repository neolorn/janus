using System;
using System.Security.Cryptography;
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
/// <param name="refusals">Where a refused code is written down.</param>
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
/// Implements OPS-BOOT-002, OPS-BOOT-004, AUTH-STEP-004, LIB-API-005 and CONV-LOG-005.
/// Generation and the read of the standing credential are the host's contract,
/// <see cref="IBreakGlass"/>; presentation and the sweep are the boundary's and the
/// worker's, and stay internal. Every attempt
/// is counted against the global limit before anything else is looked at, and in a
/// transaction of its own, so a refused attempt is counted as surely as one that
/// succeeds. A group whose check symbol does not hold is refused before any hash is
/// compared. A refused code is written to the audit trail as a failed authentication,
/// which no log level governs.
/// </remarks>
internal sealed class BreakGlassService(
    IBreakGlassStore store,
    IEmergencyAccount emergency,
    IBreakGlassAudit audit,
    ISessionAudit refusals,
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
    RandomNumberGenerator randomness) : IBreakGlass
{
    // OPS-BOOT-004: at most five attempts an hour, from all sources together.
    private const int GlobalAttempts = 5;

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
            return attempted == GlobalAttempts + 1
                && await LimitReachedAsync(now, cancellationToken).ConfigureAwait(false) is Error unraised
                ? Result.Failure<IssuedSession>(unraised)
                : Result.Failure<IssuedSession>(ThrottleService.Refusal(now + GlobalWindow));
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
            return Result.Failure<IssuedSession>(ThrottleService.Refusal(now + delay));
        }

        if (BreakGlassCode.Checked(credential) is not string canonical
            || await emergency.FindAsync(cancellationToken).ConfigureAwait(false) is not SubjectId account)
        {
            return await RefusedAsync(attempt, ErrorCodes.BreakGlassInvalid, account: null, cancellationToken)
                .ConfigureAwait(false);
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
                    account,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(presented);
        }
    }

    /// <summary>
    /// Forgets the attempts no window counts any more (OPS-OBS-003).
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were forgotten.</returns>
    public ValueTask<int> SweepAsync(CancellationToken cancellationToken) =>
        store.SweepAsync(time.GetUtcNow() - GlobalWindow, cancellationToken);

    /// <inheritdoc/>
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

        // OPS-BOOT-004 AC2: generation is raised under its own condition, scoped to the
        // issue, and reaches the owner as use does, whatever alerting.owner.enabled says.
        Result alerted = await alerts
            .RaiseAsync(
                Alerts.Of(AlertCondition.BreakGlassGenerated, issued.Id.ToString(), now),
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

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<DateTimeOffset?>> StandingAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // OPS-BOOT-001 AC3: every system administrator reads whether the deployment is
        // without its emergency credential; the read shows nothing of the credential and
        // asks for no step-up.
        if (await scope.RefusedAsync(context, Permissions.SystemAdminister, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<DateTimeOffset?>(refused);
        }

        BreakGlassCredential? standing = await store.StandingAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(standing?.IssuedAt);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // OPS-BOOT-004 AC7 and OPS-ALERT-001 (D-166, 292): the first arrival the limit
    // refuses is raised against the reserved account, in a transaction of its own, so
    // the attack is heard while the limit holds it.
    private async ValueTask<Error?> LimitReachedAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        SubjectId? account = await emergency.FindAsync(cancellationToken).ConfigureAwait(false);

        return (await alerts
                .RaiseAsync(Alerts.Of(AlertCondition.AuthFailuresSustained, account?.ToString(), now), cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
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
        // well as the operator, scoped to the issue it spent.
        Result alerted = await alerts
            .RaiseAsync(
                Alerts.Of(AlertCondition.BreakGlassUsed, standing.Id.ToString(), now),
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

    // CONV-LOG-005: a refused code is a failed authentication, written to the trail
    // against the reserved account where the refusal came after it was looked up, and
    // never with anything that was typed.
    private async ValueTask<Result<IssuedSession>> RefusedAsync(
        ThrottleAttempt attempt,
        ErrorCode refusal,
        SubjectId? account,
        CancellationToken cancellationToken)
    {
        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await refusals.FailedAsync(account, Factor.BreakGlass, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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
