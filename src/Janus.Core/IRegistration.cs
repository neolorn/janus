using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The path into an account: a session that stages what the steps collect, reserves
/// nothing, and either becomes an account in one transaction at the terms step or
/// leaves nothing behind.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, REG-SESS-001 to REG-SESS-008, REG-PROF-002 and
/// REG-IDENT-010. No operation here takes an access context: registration is what
/// happens before a principal exists, and holding the session is what a caller is
/// judged on (BFF-CSRF-005b). Nothing accepts a return destination at any step.
/// </remarks>
public interface IRegistration
{
    /// <summary>
    /// Creates a registration session for one browser, recording the application the
    /// person came from.
    /// </summary>
    /// <param name="client">The originating application's client identifier.</param>
    /// <param name="language">
    /// The locale of the request, which its messages go out in and which the account
    /// keeps as its language preference (IDN-ATTR-001).
    /// </param>
    /// <param name="source">
    /// The address the registration is started from, which the source restrictions
    /// count every message of this registration against.
    /// </param>
    /// <param name="invitationToken">
    /// The token of the invitation link the person pressed, or nothing for a public
    /// registration. The press is what verifies the email the invitation bound, and
    /// the invitation's organization governs every step from here (REG-INV-001).
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The session, whose state is read with <see cref="StateAsync"/>, or the refusal:
    /// <c>identity.invitation.expired</c> where the token opens no invitation,
    /// <c>identity.invitation.identifiermismatch</c> where the email it binds is an
    /// account's already.
    /// </returns>
    ValueTask<Result<RegistrationSessionId>> BeginAsync(
        string client,
        string language,
        string source,
        string? invitationToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// The session's state, which is the whole of what the screens render from.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The state, or the failure where no live session answers to it.</returns>
    ValueTask<Result<RegistrationState>> StateAsync(
        RegistrationSessionId session,
        CancellationToken cancellationToken);

    /// <summary>
    /// Answers the age screen. The affirmation is derived from the date; the date
    /// itself is retained only where <c>profile.dateofbirth</c> is not off.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="dateOfBirth">The date entered.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The state at the next step, or <c>identity.profile.underage</c>, which ends the
    /// session.
    /// </returns>
    ValueTask<Result<RegistrationState>> RecordAgeAsync(
        RegistrationSessionId session,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stages the identifier the current step asks for and dispatches a code and a
    /// link to it.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="kind">Which kind the step collects.</param>
    /// <param name="value">The value as the person entered it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The state, identical whether or not the value belongs to an account already.
    /// </returns>
    ValueTask<Result<RegistrationState>> StageAsync(
        RegistrationSessionId session,
        IdentifierKind kind,
        string value,
        CancellationToken cancellationToken);

    /// <summary>
    /// Skips the phone step, which is permitted only where the deployment asks for no
    /// phone.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The state at the confirm step, or the refusal.</returns>
    ValueTask<Result<RegistrationState>> SkipPhoneAsync(
        RegistrationSessionId session,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds a further email or phone at the confirm step, within the kind's maximum.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="kind">Which kind to add.</param>
    /// <param name="value">The value as the person entered it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The state, or the refusal where the maximum is reached.</returns>
    ValueTask<Result<RegistrationState>> AddAsync(
        RegistrationSessionId session,
        IdentifierKind kind,
        string value,
        CancellationToken cancellationToken);

    /// <summary>
    /// Corrects one staged identifier in place, which discards its verification and
    /// sends to the corrected value.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="identifier">Which staged identifier.</param>
    /// <param name="value">The corrected value.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The state, or the refusal where the identifier is locked.</returns>
    ValueTask<Result<RegistrationState>> ChangeAsync(
        RegistrationSessionId session,
        IdentifierId identifier,
        string value,
        CancellationToken cancellationToken);

    /// <summary>
    /// Discards an unverified extra identifier from the confirm step.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="identifier">Which staged identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The state, or the refusal where discarding it would leave the required minimum
    /// of its kind unmet.
    /// </returns>
    ValueTask<Result<RegistrationState>> DiscardAsync(
        RegistrationSessionId session,
        IdentifierId identifier,
        CancellationToken cancellationToken);

    /// <summary>
    /// Confirms one staged identifier by the code the message carried.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="identifier">Which staged identifier.</param>
    /// <param name="code">The code typed where the flow is waiting.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The state, or the failure the code produced.</returns>
    ValueTask<Result<RegistrationState>> VerifyAsync(
        RegistrationSessionId session,
        IdentifierId identifier,
        string code,
        CancellationToken cancellationToken);

    /// <summary>
    /// What a verification link does where it was opened. A press from the browser
    /// that started the flow verifies; anything else changes nothing and yields the
    /// code to type.
    /// </summary>
    /// <param name="session">
    /// The session the requesting browser carries, or nothing where it carries none.
    /// </param>
    /// <param name="linkToken">The token the message carried.</param>
    /// <param name="press">Whether the person pressed the control.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the press verified, and where it did not, what the landing shows.
    /// </returns>
    ValueTask<Result<LinkLanding>> LandAsync(
        RegistrationSessionId? session,
        string linkToken,
        bool press,
        CancellationToken cancellationToken);

    /// <summary>
    /// Completes the confirm step, which every staged identifier has to be verified
    /// for.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The state at the security step, or the refusal.</returns>
    ValueTask<Result<RegistrationState>> ConfirmAsync(
        RegistrationSessionId session,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets the password of the security step. A password below the single-factor
    /// floor leaves a second step mandatory; lengthening it lifts that in place. The
    /// step is complete, and the terms step reachable, as soon as the account would
    /// reach a primary sign-in method with nothing outstanding.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="password">The password, cleared by the caller after the call.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The state, carrying the recovery codes where setting the password is what put
    /// a second step beside one, or the refusal the floor or the blocklist produced.
    /// </returns>
    ValueTask<Result<RegistrationState>> SetPasswordAsync(
        RegistrationSessionId session,
        string password,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the terms accepted, the notice presented, the affirmation derived at
    /// the age step and the consent controls, and creates the account in one
    /// transaction.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="termsVersion">The version of the terms accepted.</param>
    /// <param name="noticeVersion">The version of the privacy notice presented.</param>
    /// <param name="consents">The consent controls, by purpose.</param>
    /// <param name="device">What the registering browser says it is.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account and the session it is signed in on, or the refusal.</returns>
    /// <remarks>
    /// INT-GEN-006: the city the session shows is resolved inside the library from the
    /// address the session was opened on, so no caller says where it was.
    /// </remarks>
    ValueTask<Result<RegistrationCompleted>> AcceptTermsAsync(
        RegistrationSessionId session,
        string termsVersion,
        string noticeVersion,
        IReadOnlyDictionary<string, bool> consents,
        DeviceDescription device,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends the session at once and leaves nothing behind. Every code and link it
    /// issued stops working.
    /// </summary>
    /// <param name="session">
    /// The session the requesting browser carries, or nothing where the token stands
    /// for it.
    /// </param>
    /// <param name="linkToken">
    /// The token of a message the session sent, which is what the control on a link
    /// opened elsewhere presents.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, whether or not either resolved to a live session.</returns>
    ValueTask<Result> AbandonAsync(
        RegistrationSessionId? session,
        string? linkToken,
        CancellationToken cancellationToken);
}
