using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Mailboxes;

/// <summary>
/// The mail app passwords of the signed-in person, created, listed and revoked at the
/// mail server with a token the library issues for them.
/// </summary>
/// <param name="server">The mail server, or nothing where the deployment hosts no mailbox.</param>
/// <param name="tokens">Where the person's token is issued.</param>
/// <param name="mailboxes">Where the mailbox the account holds is read.</param>
/// <param name="accounts">Where the account's state is read.</param>
/// <param name="stepUp">What creation and revocation ask of the session.</param>
/// <param name="identifiers">Where the security-notice set and the language are read.</param>
/// <param name="sending">What tells the security-notice set.</param>
/// <param name="configuration">Where the languages are read.</param>
/// <param name="audit">Where creation and revocation are written down.</param>
/// <param name="work">The one transaction the record of a change runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, REG-MAIL-002, INT-MAIL-010 and AUTH-OIDC-001 AC4. The library
/// stores nothing about an app password: the server generates the secret and holds the
/// credential, and what is written here is the notice and the audit row, which name the
/// server's identifier and never the secret or the label.
/// </remarks>
internal sealed class AppPasswords(
    IMailServer? server,
    IMailServerTokens tokens,
    IMailboxStore mailboxes,
    IAccountDirectory accounts,
    StepUpGuard stepUp,
    IIdentifierDirectory identifiers,
    INotificationHandler sending,
    IConfigurationStore configuration,
    ICredentialAudit audit,
    IUnitOfWork work,
    TimeProvider time) : IAppPasswords
{
    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(capacity: 0, StringComparer.Ordinal);

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<AppPassword>>> ListAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await HolderAsync(context, cancellationToken).ConfigureAwait(false)
            is not (SubjectId subject, IMailServer hosting))
        {
            return Result.Failure<IReadOnlyList<AppPassword>>(Error.From(ErrorCodes.Denied));
        }

        Error? failure = null;

        string token = (await tokens.IssueAsync(subject, session, cancellationToken).ConfigureAwait(false))
            .Match(issued => issued, error => Withheld<string>(error, ref failure));

        // INT-MAIL-010 AC2: what the server holds, read from it now and never from a copy.
        return failure is Error refused
            ? Result.Failure<IReadOnlyList<AppPassword>>(refused)
            : await hosting.AppPasswordsAsync(token, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IssuedAppPassword>> CreateAsync(
        AccessContext context,
        SessionId session,
        string label,
        DateTimeOffset? expiresAt,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(source);

        if (await HolderAsync(context, cancellationToken).ConfigureAwait(false)
            is not (SubjectId subject, IMailServer hosting))
        {
            return Result.Failure<IssuedAppPassword>(Error.From(ErrorCodes.Denied));
        }

        // REG-MAIL-002: each app password carries a label, bounded as every other
        // credential's is.
        if (!CredentialLabel.TryParse(label, out CredentialLabel named))
        {
            return Result.Failure<IssuedAppPassword>(Error.From(ErrorCodes.CredentialLabelInvalid));
        }

        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.MailCredentialCreate, cancellationToken)
                .ConfigureAwait(false)
            is Error gate)
        {
            return Result.Failure<IssuedAppPassword>(gate);
        }

        Error? failure = null;

        string token = (await tokens.IssueAsync(subject, session, cancellationToken).ConfigureAwait(false))
            .Match(issued => issued, error => Withheld<string>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedAppPassword>(failure);
        }

        // INT-MAIL-010 AC1: one call to the server, which generates the secret; the
        // library neither chooses one nor keeps the one it is handed.
        IssuedAppPassword created = (await hosting
                .CreateAppPasswordAsync(token, named.Value, expiresAt, cancellationToken)
                .ConfigureAwait(false))
            .Match(issued => issued, error => Withheld<IssuedAppPassword>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedAppPassword>(failure);
        }

        await RecordAsync(AuditActions.MailCredentialCreated, subject, created.Id, source, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(created);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RevokeAsync(
        AccessContext context,
        SessionId session,
        string id,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(source);

        if (await HolderAsync(context, cancellationToken).ConfigureAwait(false)
            is not (SubjectId subject, IMailServer hosting))
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // An identifier that names nothing names no app password of the person's.
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result.Failure(Error.From(ErrorCodes.CredentialNotFound));
        }

        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.MailCredentialRevoke, cancellationToken)
                .ConfigureAwait(false)
            is Error gate)
        {
            return Result.Failure(gate);
        }

        Error? failure = null;

        string token = (await tokens.IssueAsync(subject, session, cancellationToken).ConfigureAwait(false))
            .Match(issued => issued, error => Withheld<string>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // INT-MAIL-010 AC3: the credential goes at the server, so a client using it fails
        // on its next connection.
        if ((await hosting.RevokeAppPasswordAsync(token, id, cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure(refused);
        }

        await RecordAsync(AuditActions.MailCredentialRevoked, subject, id, source, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    private static T Withheld<T>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static SendDestination Destination(HeldIdentifier identifier) =>
        identifier.Kind is IdentifierKind.Email
            ? SendDestination.Of(EmailAddress.TryParse(identifier.Canonical, out EmailAddress address)
                ? address
                : throw new InvalidOperationException("A held address is canonical already."))
            : SendDestination.Of(PhoneNumber.TryParse(identifier.Canonical, out PhoneNumber number)
                ? number
                : throw new InvalidOperationException("A held number is canonical already."));

    // INT-MAIL-006: the operations are present only where the account holds a mailbox
    // the server has been told to enable, which is an active account holding one; a
    // deployment that hosts no mailbox has none to hold.
    private async ValueTask<(SubjectId Subject, IMailServer Server)?> HolderAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        if (server is null || context.Effective is not SubjectId subject)
        {
            return null;
        }

        if (await mailboxes.HeldByAsync(subject, cancellationToken).ConfigureAwait(false)
                is not { IsHeld: true, ReleasedAt: null }
            || await accounts.StateAsync(subject, cancellationToken).ConfigureAwait(false)
                is not AccountState.Active)
        {
            return null;
        }

        return (subject, server);
    }

    // REG-MAIL-002: creation and revocation are notified to the security-notice set and
    // audited, in one transaction, once the server has acted.
    private async ValueTask RecordAsync(
        AuditAction action,
        SubjectId subject,
        string credential,
        string source,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        _ = await TellAsync(subject, source, cancellationToken).ConfigureAwait(false);
        await audit.MailCredentialAsync(action, subject, credential, now, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> TellAsync(
        SubjectId subject,
        string source,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken).ConfigureAwait(false);
        string? settled = await identifiers.LanguageAsync(subject, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => (IReadOnlyList<string>)[]);

        string? language = RecipientLanguage.Of(settled, requested: null, languages);
        int told = 0;

        foreach (HeldIdentifier identifier in held.NoticeSet)
        {
            var request = new SendRequest(
                Destination(identifier),
                MessageKind.SecurityNotice,
                RestrictionPurpose.Notification,
                source,
                language)
            {
                Subject = subject,
                Values = Nothing,
            };

            // A security notice one destination refuses still reaches the rest: the
            // set exists so that no one channel can silence it.
            Result<SendReference> sent = await sending
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
    }
}
