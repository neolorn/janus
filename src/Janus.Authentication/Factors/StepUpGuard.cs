using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What an operation asks before it acts: whether the session in front of it has
/// already proved what its action's gate costs.
/// </summary>
/// <param name="sessions">Where the session the request arrived on is read.</param>
/// <param name="authenticators">Where the account's credentials are read.</param>
/// <param name="passwords">Where the account's password is read.</param>
/// <param name="policies">What resolves the policy the gates come from.</param>
/// <param name="time">The clock the recency is judged against.</param>
/// <remarks>
/// Implements AUTH-STEP-001, AUTH-STEP-002 and chapter 10 section 5a. The answer is
/// yes or the one refusal: what the person could present instead is the business of
/// the step-up endpoint, which the refusal sends them to.
/// </remarks>
internal sealed class StepUpGuard(
    ISessionStore sessions,
    IAuthenticatorStore authenticators,
    IPasswordStore passwords,
    PolicyResolution policies,
    TimeProvider time)
{
    /// <summary>
    /// Whether a session has proved what an action costs.
    /// </summary>
    /// <param name="subject">Whose account the action is on.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="action">Which action.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing where it has, and the refusal where it has not.</returns>
    public ValueTask<Error?> PassedAsync(
        SubjectId subject,
        SessionId session,
        StepUpAction action,
        CancellationToken cancellationToken) =>
        JudgedAsync(subject, session, action, enrolling: null, cancellationToken);

    /// <summary>
    /// Whether a session has proved what enrolling a credential costs, which is the
    /// lower of what the account can reach and what the credential itself would
    /// contribute (AUTH-STEP-007).
    /// </summary>
    /// <param name="subject">Whose account the enrolment is on.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="action">Which action, which is enrolment or the upgrade of one.</param>
    /// <param name="enrolling">The catalogue entry being created.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing where it has, and the refusal where it has not.</returns>
    public ValueTask<Error?> PassedToEnrolAsync(
        SubjectId subject,
        SessionId session,
        StepUpAction action,
        Factor enrolling,
        CancellationToken cancellationToken) =>
        JudgedAsync(subject, session, action, enrolling, cancellationToken);

    private async ValueTask<Error?> JudgedAsync(
        SubjectId subject,
        SessionId session,
        StepUpAction action,
        Factor? enrolling,
        CancellationToken cancellationToken)
    {
        Session? live = await sessions.FindAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null || live.Subject != subject)
        {
            return Error.From(ErrorCodes.StepUpRequired);
        }

        Error? failure = null;

        Policy policy = (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        if (!policy.Gates.TryGetValue(action, out Gate? gate))
        {
            return Error.From(ErrorCodes.StepUpRequired);
        }

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        Password? password = await passwords.FindAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        var held = HeldFactors.Of(enrolled, password is not null);
        DateTimeOffset now = time.GetUtcNow();

        StepUpChallenge challenge = enrolling is Factor creating
            ? StepUp.ToEnrol(live, gate, held, creating, now)
            : StepUp.On(live, gate, held, now);

        return challenge.Outcome is StepUpOutcome.Satisfied
            ? null
            : Error.From(ErrorCodes.StepUpRequired);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
