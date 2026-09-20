using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What an account may read and edit about itself: the whole of it in one read, the
/// profile, the preferences, the credentials it holds and which of them it is offered
/// first at a second step.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, REG-ACCT-001, REG-PROF-001, REG-PREF-001, REG-IDENT-009 and
/// IDN-ATTR-008. A field a policy has turned off is neither accepted nor returned, so
/// the shape of the answer follows the deployment and never the caller.
/// </remarks>
public interface IAccount
{
    /// <summary>
    /// The account in the groups of REG-ACCT-001.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account, or the refusal where it holds none.</returns>
    ValueTask<Result<AccountDetail>> ReadAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Edits the profile and, where the deployment holds usernames, the username. A
    /// username change is a step-up action and answers the step-up code where the
    /// session has not proved enough; the names are not gated.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="edit">What the edit carries.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> EditProfileAsync(
        AccessContext context,
        SessionId session,
        ProfileEdit edit,
        CancellationToken cancellationToken);

    /// <summary>
    /// The language, the time zone and the value in force for every key the host
    /// declared.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The preferences.</returns>
    ValueTask<Result<PreferenceValues>> ReadPreferencesAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the preference set. A key the host did not declare, a value its
    /// declared type refuses, a set over <c>preferences.maxsize</c> and a key only an
    /// administrator may edit are each refused by name.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="language">The language, or nothing to clear it.</param>
    /// <param name="timeZone">The time zone, or nothing to clear it.</param>
    /// <param name="declared">The values to hold, which replace whatever was held.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> SetPreferencesAsync(
        AccessContext context,
        string? language,
        string? timeZone,
        IReadOnlyDictionary<string, string> declared,
        CancellationToken cancellationToken);

    /// <summary>
    /// The credentials enrolled on the account, by property and label.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The credentials.</returns>
    ValueTask<Result<IReadOnlyList<CredentialSummary>>> ListCredentialsAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Renames a credential, which is not a step-up action: the label names a thing
    /// the person already holds.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="credential">Which credential.</param>
    /// <param name="label">The name to give it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> LabelCredentialAsync(
        AccessContext context,
        AuthenticatorId credential,
        string label,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets which enrolled second factor a second-step challenge offers first.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="credential">Which credential.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal where the account holds no such second step.</returns>
    ValueTask<Result> PreferSecondStepAsync(
        AccessContext context,
        AuthenticatorId credential,
        CancellationToken cancellationToken);
}
