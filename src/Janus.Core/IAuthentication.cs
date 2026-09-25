using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Signing in: the challenge an identifier opens, the factors presented against it,
/// the new-device check that may hold it, and the links and codes a channel carries.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTH-FACT-001 to AUTH-FACT-004, AUTH-FACT-016,
/// AUTH-FACT-017 and AUTH-STEP-001. Nothing here takes an access context except the
/// step-up, which is the one operation a principal already exists for. Every answer
/// is the same for an identifier that resolves to an account and one that resolves to
/// nothing (AUTH-ABUSE-003).
/// </remarks>
public interface IAuthentication
{
    /// <summary>
    /// Opens a sign-in for one identifier of any kind, detected and canonicalised.
    /// </summary>
    /// <param name="identifier">The email, phone or username as it was entered.</param>
    /// <param name="source">
    /// The address the attempt came from, which the progressive delay counts it
    /// against.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The challenge, the policy's enabled primary factors and a WebAuthn challenge,
    /// identical whether or not the identifier resolves to an account.
    /// </returns>
    ValueTask<Result<SignInChallenge>> BeginAsync(
        string identifier,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Presents one factor against a challenge, until what is presented reaches the
    /// assurance the policy requires.
    /// </summary>
    /// <param name="challenge">The handle <see cref="BeginAsync"/> returned.</param>
    /// <param name="presented">The factor and what proves it.</param>
    /// <param name="device">What the browser says it is.</param>
    /// <param name="source">The address the attempt came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the sign-in reached, or the refusal.</returns>
    /// <remarks>
    /// INT-GEN-006: the city a session shows is resolved inside the library from this
    /// address, so no caller says where a session was used from.
    /// </remarks>
    ValueTask<Result<SignInProgress>> PresentAsync(
        string challenge,
        FactorPresentation presented,
        DeviceDescription device,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Completes a sign-in the new-device check held, with the code sent to the
    /// account's primary email.
    /// </summary>
    /// <param name="challenge">The handle the held sign-in carries.</param>
    /// <param name="code">The code typed where the sign-in began.</param>
    /// <param name="device">What the browser says it is.</param>
    /// <param name="source">The address the attempt came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the sign-in reached, or the failure the code produced.</returns>
    ValueTask<Result<SignInProgress>> VerifyDeviceAsync(
        string challenge,
        [NeverLogged] string code,
        DeviceDescription device,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Raises a live session's assurance by presenting one more factor.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="challenge">
    /// The handle <see cref="BeginAsync"/> returned, which a ceremony signs over and
    /// which belongs to the asking principal or to nobody.
    /// </param>
    /// <param name="presented">The factor and what proves it.</param>
    /// <param name="source">
    /// The address the attempt came from, which the progressive delay counts a refused
    /// factor against, as it does at sign-in.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the session now reaches, or the refusal.</returns>
    ValueTask<Result<SignInProgress>> StepUpAsync(
        AccessContext context,
        SessionId session,
        string challenge,
        FactorPresentation presented,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sends a sign-in link to an identifier, by email or by text according to its
    /// kind.
    /// </summary>
    /// <param name="identifier">The email or phone as it was entered.</param>
    /// <param name="language">
    /// The locale of the request, which the message goes out in where the account holds
    /// no language of its own (IDN-ATTR-001).
    /// </param>
    /// <param name="source">
    /// The address the request came from, which the source restrictions count it
    /// against.
    /// </param>
    /// <param name="browser">
    /// What the requesting browser carries before it holds a session, which is what
    /// the link is completed in and nothing else.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, whether or not the identifier resolves to an account and whether or
    /// not the policy enables the link factor of its kind.
    /// </returns>
    ValueTask<Result> SendLinkAsync(
        string identifier,
        string language,
        string source,
        string? browser,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sends a one-time code to an email identifier, where the policy enables the
    /// email code.
    /// </summary>
    /// <param name="identifier">The email as it was entered.</param>
    /// <param name="language">
    /// The locale of the request, which the message goes out in where the account holds
    /// no language of its own (IDN-ATTR-001).
    /// </param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, whether or not the identifier resolves to an account.</returns>
    ValueTask<Result> SendCodeAsync(
        string identifier,
        string language,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// What a sign-in link does where it was opened. A press from the browser that
    /// requested it signs in; anything else changes nothing and yields the code to
    /// type where the sign-in began.
    /// </summary>
    /// <param name="challenge">
    /// The handle the sign-in began with, which the requesting browser holds and no
    /// other one does.
    /// </param>
    /// <param name="browser">
    /// What the requesting browser carries, or nothing where it carries none.
    /// </param>
    /// <param name="linkToken">The token the message carried.</param>
    /// <param name="press">Whether the person pressed the control.</param>
    /// <param name="device">What the browser says it is.</param>
    /// <param name="source">The address the attempt came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The sign-in, or what the landing shows instead.</returns>
    ValueTask<Result<SignInLanding>> LandAsync(
        string challenge,
        string? browser,
        [NeverLogged] string linkToken,
        bool press,
        DeviceDescription device,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// The browsers the account knows: the ones it trusts for the second step and the
    /// ones the new-device check remembers.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The browsers, or the refusal where nobody is asking.</returns>
    ValueTask<Result<IReadOnlyList<DeviceSummary>>> ListDevicesAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Forgets one browser: a trusted one is asked for the second step again, a
    /// remembered one faces the new-device check again.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="device">Which browser.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the refusal where it is not theirs to forget.</returns>
    ValueTask<Result> ForgetDeviceAsync(
        AccessContext context,
        DeviceId device,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends a pending sign-in link, which is the ending control of a link opened in a
    /// browser other than the one that requested it.
    /// </summary>
    /// <param name="linkToken">The token the message carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, whether or not the token resolved to a pending link.</returns>
    ValueTask<Result> AbandonLinkAsync([NeverLogged] string linkToken, CancellationToken cancellationToken);
}
