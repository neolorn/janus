using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
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
/// <param name="identifiers">Where the number a text would go to is read.</param>
/// <param name="signals">What is known about that number before a text factor is offered.</param>
/// <param name="time">The clock the recency is judged against.</param>
/// <remarks>
/// Implements AUTH-STEP-001, AUTH-STEP-002, AUTH-STEP-004, AUTH-FACT-002b, BFF-STEP-001,
/// OPS-BOOT-002 and chapter 10 section 5a.
/// The answer is yes or the one refusal, which carries what chapter 9 says every
/// <c>auth.stepup.required</c> carries: the gate's three values, the outcome and the
/// combinations that would meet it, so the person steps up at the step-up endpoint
/// knowing what to present.
/// </remarks>
internal sealed class StepUpGuard(
    ISessionStore sessions,
    IAuthenticatorStore authenticators,
    IPasswordStore passwords,
    PolicyResolution policies,
    IIdentifierDirectory identifiers,
    PhoneSignals signals,
    TimeProvider time)
{
    // OPS-BOOT-002: what would give the break-glass session's account a sign-in method
    // or a mailbox, or end it. The session passes every gate, and these it never passes.
    private static readonly FrozenSet<StepUpAction> Unavailable = new[]
    {
        StepUpAction.PasswordSet,
        StepUpAction.IdentifierAdd,
        StepUpAction.UsernameChange,
        StepUpAction.FactorEnrol,
        StepUpAction.ProviderLink,
        StepUpAction.RecoveryCodesGenerate,
        StepUpAction.MailCredentialCreate,
        StepUpAction.AccountDeactivate,
        StepUpAction.AccountDelete,
    }.ToFrozenSet();

    /// <summary>
    /// The refusal an action OPS-BOOT-002 withholds from the reserved account meets in
    /// the break-glass session, or in a session another application opened from it,
    /// which carries the same reason (BFF-SESS-006).
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="action">Which action the operation is.</param>
    /// <returns><c>authz.denied</c> where the action is withheld; otherwise nothing.</returns>
    /// <remarks>
    /// D-179: the refusal is the context's, so an operation makes it at its gate step,
    /// before anything is loaded, and answers the same whatever the reserved account
    /// holds or lacks. It is not the step-up judgement, which the session satisfies.
    /// </remarks>
    public static Error? RefusedInBreakGlass(AccessContext context, StepUpAction action)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.BreakGlassReason is not null && Unavailable.Contains(action)
            ? Error.From(ErrorCodes.Denied)
            : null;
    }

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

    /// <summary>
    /// What a session's proof amounts to against an action's gate, for an operation
    /// that decides for itself whether the gate applies, as a configuration change does
    /// by its direction.
    /// </summary>
    /// <param name="subject">Whose account the action is on.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="action">Which action.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The challenge, met or not, or the refusal where the session is not the
    /// account's or the policy names no such gate.
    /// </returns>
    public ValueTask<Result<StepUpChallenge>> ChallengeAsync(
        SubjectId subject,
        SessionId session,
        StepUpAction action,
        CancellationToken cancellationToken) =>
        ChallengedAsync(subject, session, action, enrolling: null, Asking.Named, cancellationToken);

    /// <summary>
    /// What a session's proof amounts to against a gate named rather than enumerated:
    /// one of chapter 10 section 5a, or one the host bound its own action to.
    /// </summary>
    /// <param name="subject">Whose account the action is on.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="gate">The gate's name.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The challenge, met or not, or the refusal where the session is not the
    /// account's.
    /// </returns>
    /// <remarks>
    /// A gate the host names is one no policy states values for, so it costs what the
    /// dearest gate of the principal's policy costs (AUTHZ-GATE-005, D-160).
    /// </remarks>
    public ValueTask<Result<StepUpChallenge>> ChallengeAsync(
        SubjectId subject,
        SessionId session,
        string gate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gate);

        return ChallengedAsync(subject, session, Named(gate), enrolling: null, Asking.Named, cancellationToken);
    }

    /// <summary>
    /// What a session's proof amounts to at a step-up that names no action, once the
    /// carrier's signal has withheld the entries a text carries: the session judged
    /// against the strictest of the policy's gates, field by field, and the combinations
    /// that reach the weakest of them left without those entries.
    /// </summary>
    /// <param name="subject">Whose account is stepping up.</param>
    /// <param name="session">The session the step-up raises.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The challenge, met or not, or the refusal where the session is not the
    /// account's.
    /// </returns>
    /// <remarks>
    /// Implements AUTH-FACT-002 AC7 and AUTH-STEP-002 (D-187, D-197). The signal was
    /// asked, and its consideration recorded, at the ask that came here; it is not
    /// asked again.
    /// </remarks>
    public ValueTask<Result<StepUpChallenge>> ChallengeWithoutTextsAsync(
        SubjectId subject,
        SessionId session,
        CancellationToken cancellationToken) =>
        ChallengedAsync(subject, session, action: null, enrolling: null, Asking.UnnamedWithoutTexts, cancellationToken);

    /// <summary>
    /// What a session's proof amounts to at a step-up that names no action: the session
    /// judged against the strictest of the policy's gates, field by field, and, where it
    /// does not meet that, the combinations that reach the weakest of them, field by
    /// field, or the one of three answers an account that can present none is given,
    /// with the weakest gate's values.
    /// </summary>
    /// <param name="subject">Whose account is stepping up.</param>
    /// <param name="session">The session the step-up raises.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The challenge, met or not, or the refusal where the session is not the
    /// account's.
    /// </returns>
    /// <remarks>
    /// Implements AUTH-STEP-002 steps 1 to 3 as a step-up reads them (D-187, D-190,
    /// D-197). It asks the carrier's signal of a number a combination on offer would
    /// text, so it is asked outside any unit of work (AUTH-FACT-002b).
    /// </remarks>
    public ValueTask<Result<StepUpChallenge>> ChallengeUnnamedAsync(
        SubjectId subject,
        SessionId session,
        CancellationToken cancellationToken) =>
        ChallengedAsync(subject, session, action: null, enrolling: null, Asking.Unnamed, cancellationToken);

    /// <summary>
    /// Whether a session, as its caller holds it and has just raised it, has reached the
    /// strictest of the policy's gates, field by field, which a step-up that names no
    /// action goes on asking for until it is met.
    /// </summary>
    /// <param name="live">The session, as raised.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the gate is met, or the failure where the policy cannot be read.</returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    /// <remarks>
    /// Implements AUTH-STEP-002 step 2 (D-190). It judges the session it is handed and
    /// reads no other, and asks no signal, so the step-up decides inside its unit of
    /// work whether the challenge that holds the accepted factors is done with.
    /// </remarks>
    public async ValueTask<Result<bool>> ReachedAsync(Session live, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(live);

        Error? failure = null;

        Policy policy = (await policies.ForAsync(live.Subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        if (failure is not null || Costs(policy, action: null) is not Gate gate)
        {
            return Result.Failure<bool>(failure ?? Error.From(ErrorCodes.StepUpRequired));
        }

        return Result.Success(StepUpRefusal.Met(StepUp.On(
            live,
            gate,
            await HeldAsync(live.Subject, policy, cancellationToken).ConfigureAwait(false),
            time.GetUtcNow())));
    }

    /// <summary>
    /// What a named gate costs under the principal's policy, which a gate judged from a
    /// host's report of the caller's session reads (LIB-HOST-004).
    /// </summary>
    /// <param name="subject">The principal.</param>
    /// <param name="gate">The gate's name.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The gate's three values, a gate the host names costing what the dearest gate of
    /// the policy costs (AUTHZ-GATE-005, D-160), or the failure where the policy cannot
    /// be read or names no such gate.
    /// </returns>
    public async ValueTask<Result<Gate>> CostAsync(
        SubjectId subject,
        string gate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gate);

        Error? failure = null;

        Policy policy = (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<Gate>(failure);
        }

        return Costs(policy, Named(gate)) is Gate cost
            ? Result.Success(cost)
            : Result.Failure<Gate>(Error.From(ErrorCodes.StepUpRequired));
    }

    // A name chapter 10 section 5a lists is that action's gate; any other is one the
    // host bound its own action to.
    private static StepUpAction? Named(string gate) =>
        Enum.GetValues<StepUpAction>().Where(action => WrittenName.Of(action) == gate).ToArray() is [StepUpAction named]
            ? named
            : null;

    private async ValueTask<Error?> JudgedAsync(
        SubjectId subject,
        SessionId session,
        StepUpAction action,
        Factor? enrolling,
        CancellationToken cancellationToken) =>
        (await ChallengedAsync(subject, session, action, enrolling, Asking.Named, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(
                challenge => StepUpRefusal.Met(challenge) ? null : StepUpRefusal.Of(challenge),
                error => error);

    private async ValueTask<Result<StepUpChallenge>> ChallengedAsync(
        SubjectId subject,
        SessionId session,
        StepUpAction? action,
        Factor? enrolling,
        Asking asking,
        CancellationToken cancellationToken)
    {
        Session? live = await sessions.FindAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null || live.Subject != subject)
        {
            return Result.Failure<StepUpChallenge>(Error.From(ErrorCodes.StepUpRequired));
        }

        Error? failure = null;

        Policy policy = (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<StepUpChallenge>(failure);
        }

        if (Costs(policy, action) is not Gate gate)
        {
            return Result.Failure<StepUpChallenge>(Error.From(ErrorCodes.StepUpRequired));
        }

        // AUTH-STEP-002 step 2 (D-197): a step-up names no action, so what it offers,
        // and what it answers an account that can present nothing with, is read from
        // the weakest of the policy's gates, field by field. A gate that is named is
        // judged and offered against the one gate it costs.
        Gate offering = asking is Asking.Named
            ? gate
            : policy.Gates.Values.Aggregate(PolicyStrictness.Weakest);

        HeldFactors held = await HeldAsync(subject, policy, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = time.GetUtcNow();
        StepUpChallenge challenge = Challenged(live, gate, offering, held, enrolling, now);

        // AUTH-FACT-002b AC6: where the carrier reports a recent change of SIM or of
        // network for the number, the entries a text carries are withheld from the
        // combinations offered; where none is left, the answer is the one an account
        // that cannot reach the gate is given, and never a pass. An ask the signal
        // already refused withholds every such entry the account could present, the one
        // number carrying them all, and asks nothing again.
        IReadOnlySet<Factor> withheld = asking is Asking.UnnamedWithoutTexts
            ? held.Usable.Where(usable => FactorCatalogue.Of(usable).Restricted).ToFrozenSet()
            : challenge.Outcome is StepUpOutcome.Present
                ? await WithheldAsync(subject, challenge.Combinations, cancellationToken).ConfigureAwait(false)
                : FrozenSet<Factor>.Empty;

        return Result.Success(withheld.Count is 0
            ? challenge
            : Challenged(
                live,
                gate,
                offering,
                held with { Usable = held.Usable.Except(withheld).ToFrozenSet() },
                enrolling,
                now));
    }

    // AUTH-STEP-002 step 2: a factor the policy in force does not permit is not one the
    // account can present at a gate, so no combination offered holds it. What the
    // account can reach (AUTH-STEP-006) is read from its credentials alone.
    private async ValueTask<HeldFactors> HeldAsync(
        SubjectId subject,
        Policy policy,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        Password? password = await passwords.FindAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        var held = HeldFactors.Of(enrolled, password is not null);

        return held with { Usable = held.Usable.Intersect(policy.LoginFactors).ToFrozenSet() };
    }

    // AUTH-STEP-002 steps 1 to 3: the session is judged against the gate. Where it does
    // not meet it and the offer is read from another gate, as a step-up's is from the
    // weakest of the policy's (D-197), the combinations offered and the answer given an
    // account that can present none are that gate's, at the level it is read at for
    // the account, whether or not the session meets that gate.
    private static StepUpChallenge Challenged(
        Session live,
        Gate gate,
        Gate offering,
        HeldFactors held,
        Factor? enrolling,
        DateTimeOffset now)
    {
        if (enrolling is Factor creating)
        {
            return StepUp.ToEnrol(live, gate, held, creating, now);
        }

        StepUpChallenge judged = StepUp.On(live, gate, held, now);

        if (offering == gate || StepUpRefusal.Met(judged))
        {
            return judged;
        }

        StepUpChallenge offered = StepUp.On(live, offering, held, now);

        return (StepUpRefusal.Met(offered)
            ? StepUpOffer.To(
                held,
                offered.Required,
                offered.PhishingResistant,
                offered.MaximumAge,
                StepUp.Reachable(held.Standing))
            : offered) with
        {
            Downgraded = judged.Downgraded,
        };
    }

    // The signal is asked about the entries a combination on offer would text, and
    // about nothing else, so a gate already met or met without a text asks nothing.
    private async ValueTask<IReadOnlySet<Factor>> WithheldAsync(
        SubjectId subject,
        IReadOnlyList<IReadOnlyList<Factor>> combinations,
        CancellationToken cancellationToken)
    {
        Factor[] textable = [.. combinations
            .SelectMany(combination => combination)
            .Distinct()
            .Where(offered => FactorCatalogue.Of(offered).Restricted)];

        if (textable.Length is 0)
        {
            return FrozenSet<Factor>.Empty;
        }

        HeldIdentifiers numbers = await identifiers.HeldAsync(subject, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Authenticator> enrolled = await authenticators.OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);
        var withheld = new HashSet<Factor>();

        foreach (Factor carried in textable)
        {
            if (!await signals.AllowsAsync(carried, numbers, enrolled, subject, cancellationToken)
                    .ConfigureAwait(false))
            {
                _ = withheld.Add(carried);
            }
        }

        return withheld;
    }

    // D-160: a host's gate has no values of its own in any policy, so it asks what the
    // dearest of the policy's gates asks and never less than a named one would.
    private static Gate? Costs(Policy policy, StepUpAction? action) =>
        action is StepUpAction named
            ? policy.Gates.TryGetValue(named, out Gate? gate) ? gate : null
            : policy.Gates.Values.Aggregate(PolicyStrictness.Strictest);

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // What a challenge is asked for: a gate that is named, by the catalogue or by a
    // host, or a step-up, which names none, with or without the entries a text carries.
    private enum Asking
    {
        Named = 0,
        Unnamed = 1,
        UnnamedWithoutTexts = 2,
    }
}
