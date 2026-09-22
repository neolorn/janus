using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sessions;

/// <summary>
/// Everything that happens to a session: it begins at an authentication, it is
/// resolved and refreshed on use, it records what a later combination proved, and it
/// ends.
/// </summary>
/// <param name="sessions">Where sessions are read and written.</param>
/// <param name="audit">Where the factors presented are recorded.</param>
/// <param name="policies">Where the principal's policy is resolved.</param>
/// <param name="configuration">Where the lifetimes are read from.</param>
/// <param name="gate">Where a permission is evaluated.</param>
/// <param name="locations">What the address a session was used from resolves to.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a session secret is drawn from.</param>
/// <remarks>
/// Implements AUTH-SESS-001 to AUTH-SESS-013. Lifetimes are read from the assurance
/// the principal's policy requires and from nothing else, so the session a passkey
/// opened lives exactly as long as the one a password opened.
/// </remarks>
internal sealed class SessionService(
    ISessionStore sessions,
    ISessionAudit audit,
    PolicyResolution policies,
    IConfigurationStore configuration,
    IAccessGate gate,
    ILocationResolver locations,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness) : ISessions
{
    /// <summary>
    /// Begins a session from an authentication.
    /// </summary>
    /// <param name="subject">Who signed in.</param>
    /// <param name="presented">What they presented.</param>
    /// <param name="origin">Where from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The session and its secret, or the failure where a factor the policy does not
    /// admit was presented or the combination falls short of the assurance the policy
    /// requires.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public ValueTask<Result<IssuedSession>> BeginAsync(
        SubjectId subject,
        IReadOnlyCollection<Factor> presented,
        SessionOrigin origin,
        CancellationToken cancellationToken) =>
        BeginAsync(subject, presented, origin, Admission.Held, cancellationToken);

    /// <summary>
    /// Begins a session that passes every gate and the stated floor for its lifetime,
    /// which the emergency path uses and nothing else does. The floor does not hold
    /// it, because the credential behind it is one printed secret and enforcing the
    /// floor would refuse the session the emergency exists for; the compensating
    /// control is the alerting.
    /// </summary>
    /// <param name="subject">Who signed in.</param>
    /// <param name="presented">What they presented.</param>
    /// <param name="origin">Where from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session and its secret.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public ValueTask<Result<IssuedSession>> BeginExemptAsync(
        SubjectId subject,
        IReadOnlyCollection<Factor> presented,
        SessionOrigin origin,
        CancellationToken cancellationToken) =>
        BeginAsync(subject, presented, origin, Admission.Exempt, cancellationToken);

    /// <summary>
    /// Begins a session for an account inside the run-up a raised requirement carries,
    /// which the raised floor does not refuse. What the session then reaches is what
    /// was presented, so every gate above it holds as it did before (AUTH-FACT-017,
    /// AUTH-SESS-009).
    /// </summary>
    /// <param name="subject">Who signed in.</param>
    /// <param name="presented">What they presented.</param>
    /// <param name="origin">Where from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The session and its secret, or the failure where a factor the policy does not
    /// admit was presented.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public ValueTask<Result<IssuedSession>> BeginDuringGraceAsync(
        SubjectId subject,
        IReadOnlyCollection<Factor> presented,
        SessionOrigin origin,
        CancellationToken cancellationToken) =>
        BeginAsync(subject, presented, origin, Admission.WithinTheRunUp, cancellationToken);

    /// <summary>
    /// The session a presented secret belongs to, refreshed by the use that resolved
    /// it.
    /// </summary>
    /// <param name="secret">What the cookie carried.</param>
    /// <param name="origin">Where the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The session, or <c>auth.session.expired</c> naming what it asks for.
    /// </returns>
    /// <exception cref="ArgumentNullException">The origin is absent.</exception>
    public async ValueTask<Result<Session>> ResolveAsync(
        OpaqueToken secret,
        SessionOrigin origin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origin);

        Session? session = await sessions
            .FindByFingerprintAsync(secret.Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return Result.Failure<Session>(Expiry(ReauthenticationKind.Full));
        }

        Error? failure = null;

        Policy policy = (await policies.ForAsync(session.Subject, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<Session>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();
        Session? spine = session.Spine == session.Id
            ? session
            : await sessions.FindAsync(session.Spine, cancellationToken).ConfigureAwait(false);

        if (spine is null)
        {
            return Result.Failure<Session>(Expiry(ReauthenticationKind.Full));
        }

        ReauthenticationKind? asked =
            SessionClock.Expired(spine, now, policy.RequiredAssurance)
            ?? SessionClock.Expired(session, now, policy.RequiredAssurance);

        if (asked is not null)
        {
            return Result.Failure<Session>(Expiry(asked.Value));
        }

        (TimeSpan inactivity, TimeSpan _) =
            await LifetimesAsync(policy, cancellationToken).ConfigureAwait(false);

        SessionOrigin used = await LocatedAsync(origin, cancellationToken).ConfigureAwait(false);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        session.Touch(used, now, inactivity);
        await sessions.RecordAsync(session, cancellationToken).ConfigureAwait(false);

        // Use of a session standing on the record is use of the record: one session
        // held three ways, and each use refreshes what lapses without use
        // (AUTH-SESS-004, AUTH-SESS-005).
        if (!ReferenceEquals(spine, session))
        {
            spine.Touch(used, now, inactivity);
            await sessions.RecordAsync(spine, cancellationToken).ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(session);
    }

    /// <summary>
    /// A combination was presented on a live session. The session records exactly
    /// what it reached and answers to a new secret, the one before it invalidated.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="presented">What was presented.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The new secret, or the failure where nothing was presented.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<IssuedSession>> PresentAsync(
        Session session,
        IReadOnlyCollection<Factor> presented,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(presented);

        if (presented.Count == 0)
        {
            return Result.Failure<IssuedSession>(Error.From(ErrorCodes.FactorRequired));
        }

        DateTimeOffset now = time.GetUtcNow();
        var secret = OpaqueToken.Draw(randomness);
        var token = OpaqueToken.Draw(randomness);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        // A combination that proves nothing here, a second factor with no first
        // factor of ours beside it or an email factor, moves nothing and is still
        // recorded.
        if (Assurance.Proved(Properties(presented)) is Assurance reached)
        {
            session.Present(reached, now);
        }

        await sessions.RecordAsync(session, cancellationToken).ConfigureAwait(false);
        await sessions
            .ReplaceSecretAsync(session.Id, secret.Fingerprint(), token.Fingerprint(), cancellationToken)
            .ConfigureAwait(false);
        await audit.PresentedAsync(session.Id, session.Subject, presented, now, cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new IssuedSession(session.Id, secret, token));
    }

    /// <summary>
    /// Restores a session that lapsed for inactivity inside its absolute window, on
    /// one factor bound to the secret the browser still holds.
    /// </summary>
    /// <param name="secret">What the cookie carried.</param>
    /// <param name="presented">What was presented.</param>
    /// <param name="origin">Where the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The session and its new secret, or the failure where the window has passed,
    /// where the policy does not admit the allowance, or where what was presented
    /// proves nothing on a session that already exists.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<IssuedSession>> RestoreAsync(
        OpaqueToken secret,
        IReadOnlyCollection<Factor> presented,
        SessionOrigin origin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presented);
        ArgumentNullException.ThrowIfNull(origin);

        Session? session = await sessions
            .FindByFingerprintAsync(secret.Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return Result.Failure<IssuedSession>(Expiry(ReauthenticationKind.Full));
        }

        Error? failure = null;

        Policy policy = (await policies.ForAsync(session.Subject, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedSession>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        // The allowance is written for a short gap inside the absolute window, which
        // is why a ninety-day one does not reach it (AUTH-SESS-005 AC3a).
        if (SessionClock.Expired(session, now, policy.RequiredAssurance)
            is not ReauthenticationKind.SingleFactor)
        {
            return Result.Failure<IssuedSession>(Expiry(ReauthenticationKind.Full));
        }

        // One factor bound to the session secret, which a second factor alone, a
        // social credential and an email factor are not.
        if (Assurance.Proved(Properties(presented)) is not { Level: >= AssuranceLevel.Aal1 } proved)
        {
            return Result.Failure<IssuedSession>(Error.From(ErrorCodes.FactorRequired));
        }

        (TimeSpan inactivity, TimeSpan _) =
            await LifetimesAsync(policy, cancellationToken).ConfigureAwait(false);

        var restored = OpaqueToken.Draw(randomness);
        var restoredToken = OpaqueToken.Draw(randomness);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        session.Present(proved, now);
        session.Touch(
            await LocatedAsync(origin, cancellationToken).ConfigureAwait(false),
            now,
            inactivity);
        await sessions.RecordAsync(session, cancellationToken).ConfigureAwait(false);
        await sessions
            .ReplaceSecretAsync(
                session.Id,
                restored.Fingerprint(),
                restoredToken.Fingerprint(),
                cancellationToken)
            .ConfigureAwait(false);
        await audit.PresentedAsync(session.Id, session.Subject, presented, now, cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new IssuedSession(session.Id, restored, restoredToken));
    }

    /// <summary>
    /// Issues a new secret for a session without changing what it proved, which is
    /// what a privilege change asks for.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The new secret.</returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public async ValueTask<Result<IssuedSession>> RotateAsync(
        Session session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        var secret = OpaqueToken.Draw(randomness);
        var token = OpaqueToken.Draw(randomness);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await sessions
            .ReplaceSecretAsync(session.Id, secret.Fingerprint(), token.Fingerprint(), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new IssuedSession(session.Id, secret, token));
    }

    /// <summary>
    /// A session of another kind standing on a live record, which inherits what the
    /// record proved and ends when it does.
    /// </summary>
    /// <param name="spine">The record it stands on.</param>
    /// <param name="type">Which kind.</param>
    /// <param name="origin">Where it begins.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The session and its secret, or the failure where the record is gone or lapsed.
    /// </returns>
    /// <exception cref="ArgumentNullException">The origin is absent.</exception>
    public async ValueTask<Result<IssuedSession>> DeriveAsync(
        SessionId spine,
        SessionType type,
        SessionOrigin origin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origin);

        Session? record = await sessions.FindAsync(spine, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return Result.Failure<IssuedSession>(Expiry(ReauthenticationKind.Full));
        }

        Error? failure = null;

        Policy policy = (await policies.ForAsync(record.Subject, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedSession>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        if (SessionClock.Expired(record, now, policy.RequiredAssurance) is ReauthenticationKind asked)
        {
            return Result.Failure<IssuedSession>(Expiry(asked));
        }

        (TimeSpan inactivity, TimeSpan _) =
            await LifetimesAsync(policy, cancellationToken).ConfigureAwait(false);

        Session derived = record.Derive(
            SessionId.New(time),
            type,
            await LocatedAsync(origin, cancellationToken).ConfigureAwait(false),
            now,
            inactivity);
        var secret = OpaqueToken.Draw(randomness);
        var token = OpaqueToken.Draw(randomness);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await sessions
            .AddAsync(derived, secret.Fingerprint(), token.Fingerprint(), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new IssuedSession(derived.Id, secret, token));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<SessionDetail>> ReadAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        Session? live = await sessions.FindAsync(session, cancellationToken).ConfigureAwait(false);

        if (context.Effective is not SubjectId subject
            || live is null
            || live.Subject != subject
            || live.EndedAt is not null)
        {
            return Result.Failure<SessionDetail>(Error.From(ErrorCodes.SessionExpired));
        }

        return Result.Success(new SessionDetail(
            live.Subject,
            live.Attained,
            live.PhishingResistant,
            live.AttainedAt,
            live.IdleExpiry < live.AbsoluteExpiry ? live.IdleExpiry : live.AbsoluteExpiry));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<SessionSummary>>> ListAsync(
        AccessContext context,
        SessionId current,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure<IReadOnlyList<SessionSummary>>(Error.From(ErrorCodes.Denied));
        }

        IReadOnlyList<Session> live = await sessions
            .LiveOfAsync(subject, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<SessionSummary>>(
        [
            .. live.Select(session => new SessionSummary(
                session.Id,
                session.CreatedAt,
                session.LastSeenAt,
                session.LastSeen.Device,
                session.LastSeen.Location,
                session.Id == current)),
        ]);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> EndAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        Session? ending = await sessions.FindAsync(session, cancellationToken).ConfigureAwait(false);

        // A session that is not the caller's is answered as one that does not exist:
        // the identifier of somebody else's session tells the caller nothing.
        if (context.Effective is not SubjectId subject
            || ending is null
            || ending.Subject != subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await sessions.EndSpineAsync(ending.Spine, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> EndEverywhereAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Effective is SubjectId subject
            ? await EndAccountAsync(subject, cancellationToken).ConfigureAwait(false)
            : Result.Failure(Error.From(ErrorCodes.Denied));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RevokeAccountAsync(
        AccessContext context,
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        Error? refused = await RefusedAsync(
                context,
                Permissions.SessionRevokeAccount,
                organization,
                cancellationToken)
            .ConfigureAwait(false);

        return refused is not null
            ? Result.Failure(refused)
            : await EndAccountAsync(subject, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RevokeEveryAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        Error? refused = await RefusedAsync(
                context,
                Permissions.SessionRevoke,
                organization,
                cancellationToken)
            .ConfigureAwait(false);

        if (refused is not null)
        {
            return Result.Failure(refused);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await sessions.EndEveryAsync(time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Ends every session of an account, which is what a transition out of
    /// <c>active</c> does in the same operation as the transition.
    /// </summary>
    /// <param name="subject">Whose sessions.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success.</returns>
    public async ValueTask<Result> EndAccountAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await sessions.EndAccountAsync(subject, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static FactorProperties[] Properties(IReadOnlyCollection<Factor> presented)
    {
        List<FactorProperties> properties = [];

        foreach (Factor factor in presented)
        {
            properties.Add(FactorCatalogue.Of(factor));
        }

        return [.. properties];
    }

    // The stated floor holds every session but the emergency one, whose single printed
    // secret the floor would refuse (AUTH-SESS-005b). A federated sign-in asserts no
    // tier of ours to hold against a raised floor, so it is admitted at the base floor
    // the policy's login factors already admit it at, and refused above it; what it may
    // then do is the gates' to say (AUTH-SESS-005a, AUTH-STEP-005).
    private static bool Admits(Policy policy, Assurance reached) =>
        reached.Level >= policy.RequiredAssurance
        || (reached.Level is AssuranceLevel.Delegated
            && policy.RequiredAssurance is AssuranceLevel.Aal1);

    // How far the floor holds a session's start. Every ordinary sign-in is held by it;
    // a sign-in inside the run-up a raised requirement carries is not, and reaches what
    // it presented (AUTH-FACT-017); the emergency path passes every gate for the
    // session's lifetime (AUTH-SESS-005b).
    private enum Admission
    {
        Held,
        WithinTheRunUp,
        Exempt,
    }

    private static Error Expiry(ReauthenticationKind asked) =>
        Error.From(
            ErrorCodes.SessionExpired,
            "reauthenticate",
            JsonSerializer.SerializeToElement(
                asked is ReauthenticationKind.SingleFactor ? "single-factor" : "full"));

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await gate.RequireAsync(context, permission, organization, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);

    private async ValueTask<(TimeSpan Inactivity, TimeSpan Absolute)> LifetimesAsync(
        Policy policy,
        CancellationToken cancellationToken)
    {
        bool raised = policy.RequiredAssurance >= AssuranceLevel.Aal2;
        DurationSetting inactivity =
            raised ? Settings.SessionAal2Inactivity : Settings.SessionDefaultInactivity;
        DurationSetting absolute =
            raised ? Settings.SessionAal2Absolute : Settings.SessionDefaultAbsolute;

        return (
            await ReadAsync(inactivity, cancellationToken).ConfigureAwait(false),
            await ReadAsync(absolute, cancellationToken).ConfigureAwait(false));
    }

    private async ValueTask<TimeSpan> ReadAsync(
        DurationSetting setting,
        CancellationToken cancellationToken) =>
        (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false))
            .Match(value => value, _ => setting.Default);

    // INT-GEN-006: the address is the session's own and the city is what the local
    // database makes of it, so nothing outside the library writes a place into one.
    private async ValueTask<SessionOrigin> LocatedAsync(
        SessionOrigin origin,
        CancellationToken cancellationToken) =>
        origin with
        {
            Location = await locations
                .ResolveAsync(origin.Address, cancellationToken)
                .ConfigureAwait(false),
        };

    private async ValueTask<Result<IssuedSession>> BeginAsync(
        SubjectId subject,
        IReadOnlyCollection<Factor> presented,
        SessionOrigin origin,
        Admission admission,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presented);
        ArgumentNullException.ThrowIfNull(origin);

        Error? failure = null;
        bool satisfiesEveryGate = admission is Admission.Exempt;

        Policy policy = (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedSession>(failure);
        }

        if (!satisfiesEveryGate && presented.Except(policy.LoginFactors).Any())
        {
            return Result.Failure<IssuedSession>(Error.From(ErrorCodes.FactorNotPermitted));
        }

        Assurance? reached = Assurance.Reached(Properties(presented));

        if (reached is null)
        {
            return Result.Failure<IssuedSession>(Error.From(ErrorCodes.FactorRequired));
        }

        if (admission is Admission.Held && !Admits(policy, reached.Value))
        {
            return Result.Failure<IssuedSession>(Error.From(ErrorCodes.FactorRequired));
        }

        (TimeSpan inactivity, TimeSpan absolute) =
            await LifetimesAsync(policy, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = time.GetUtcNow();
        var session = Session.Begin(
            SessionId.New(time),
            subject,
            reached.Value,
            await LocatedAsync(origin, cancellationToken).ConfigureAwait(false),
            now,
            inactivity,
            absolute,
            satisfiesEveryGate);
        var secret = OpaqueToken.Draw(randomness);
        var token = OpaqueToken.Draw(randomness);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await sessions
            .AddAsync(session, secret.Fingerprint(), token.Fingerprint(), cancellationToken)
            .ConfigureAwait(false);
        await audit.PresentedAsync(session.Id, subject, presented, now, cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new IssuedSession(session.Id, secret, token));
    }
}
