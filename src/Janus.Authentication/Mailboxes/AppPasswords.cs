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
/// <param name="inUse">The mail server in use, where the deployment has one.</param>
/// <param name="tokens">Where the person's token is issued.</param>
/// <param name="mailboxes">Where the mailbox the account holds is read.</param>
/// <param name="accounts">Where the account's state is read.</param>
/// <param name="restriction">Whether the account's processing is restricted.</param>
/// <param name="stepUp">What creation and revocation ask of the session.</param>
/// <param name="identifiers">Where the security-notice set and the language are read.</param>
/// <param name="sending">What tells the security-notice set.</param>
/// <param name="configuration">Where the languages are read.</param>
/// <param name="audit">Where creation and revocation are written down.</param>
/// <param name="log">Where a password the server would not revoke is written down.</param>
/// <param name="work">The one transaction the record of a change runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, REG-MAIL-002, INT-MAIL-010, IDN-ACCT-007, AUTHZ-GATE-006 and
/// AUTH-OIDC-001 AC4. The library stores nothing about an app password: the server generates the secret and holds the
/// credential, and what is written here is the notice and the audit row, which name the
/// server's identifier and never the secret or the label.
/// </remarks>
internal sealed class AppPasswords(
    IMailServerInUse inUse,
    IMailServerTokens tokens,
    IMailboxStore mailboxes,
    IAccountDirectory accounts,
    ISettingsRestriction restriction,
    StepUpGuard stepUp,
    IIdentifierDirectory identifiers,
    IGovernedSend sending,
    IConfigurationStore configuration,
    ICredentialAudit audit,
    IAppPasswordLog log,
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

        Error? failure = null;

        (SubjectId subject, IMailServer hosting) = (await HolderAsync(context, cancellationToken).ConfigureAwait(false))
            .Match(held => held, error => Withheld<(SubjectId, IMailServer)>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<AppPassword>>(failure);
        }

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

        // OPS-BOOT-002, D-179: the break-glass session creates no mail credential, and
        // is told so before anything is read, whatever the reserved account lacks.
        if (StepUpGuard.RefusedInBreakGlass(context, StepUpAction.MailCredentialCreate) is Error withheld)
        {
            return Result.Failure<IssuedAppPassword>(withheld);
        }

        Error? failure = null;

        (SubjectId subject, IMailServer hosting) = (await HolderAsync(context, cancellationToken).ConfigureAwait(false))
            .Match(held => held, error => Withheld<(SubjectId, IMailServer)>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedAppPassword>(failure);
        }

        // IDN-ACCT-007 and REG-MAIL-002: a restricted account keeps its mailbox and the
        // app passwords it has, and adds none.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error restricted)
        {
            return Result.Failure<IssuedAppPassword>(restricted);
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

        DateTimeOffset now = time.GetUtcNow();

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(_ => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        // AUTHZ-GATE-006, D-186: the server's creation is no write of the library's
        // database, so the gate is asked again inside the unit of work that records it,
        // with the acting account's row held, before that unit of work's first write.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            // INT-MAIL-010: what the server created is revoked there before the refusal
            // is answered, and its secret goes nowhere. A revocation the server does not
            // take leaves the password listed for its holder to revoke, and is logged.
            (await hosting.RevokeAppPasswordAsync(token, created.Id, cancellationToken).ConfigureAwait(false))
                .Switch(() => { }, unrevoked => log.RevocationFailed(subject, unrevoked.Code));

            return Result.Failure<IssuedAppPassword>(since);
        }

        _ = await TellAsync(subject, source, cancellationToken).ConfigureAwait(false);
        await audit
            .MailCredentialAsync(AuditActions.MailCredentialCreated, subject, created.Id, now, cancellationToken)
            .ConfigureAwait(false);
        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        return Result.Success(created);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RevokeAsync(
        AccessContext context,
        SessionId session,
        AppPasswordId id,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        Error? failure = null;

        (SubjectId subject, IMailServer hosting) = (await HolderAsync(context, cancellationToken).ConfigureAwait(false))
            .Match(held => held, error => Withheld<(SubjectId, IMailServer)>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.MailCredentialRevoke, cancellationToken)
                .ConfigureAwait(false)
            is Error gate)
        {
            return Result.Failure(gate);
        }

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

    // INT-MAIL-006 and REG-MAIL-002: the operations are present only where the account
    // holds a mailbox the server has been told to enable, which is an active or
    // restricted account holding one; a deployment that registers no mail server has
    // none to hold. A context naming no account is refused as any such context is.
    private async ValueTask<Result<(SubjectId Subject, IMailServer Server)>> HolderAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure<(SubjectId, IMailServer)>(Error.From(ErrorCodes.Denied));
        }

        if (inUse.Chosen().Match<IMailServer?>(chosen => chosen, _ => null) is not IMailServer server
            || await mailboxes.HeldByAsync(subject, cancellationToken).ConfigureAwait(false)
                is not { IsHeld: true, StandsForAddress: true }
            || await accounts.StateAsync(subject, cancellationToken).ConfigureAwait(false)
                is not (AccountState.Active or AccountState.Restricted))
        {
            return Result.Failure<(SubjectId, IMailServer)>(Error.From(ErrorCodes.MailboxNotFound));
        }

        return Result.Success((subject, server));
    }

    // REG-MAIL-002: creation and revocation are notified to the security-notice set and
    // audited, in one transaction, once the server has acted.
    private async ValueTask RecordAsync(
        AuditAction action,
        SubjectId subject,
        AppPasswordId credential,
        string source,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(_ => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        _ = await TellAsync(subject, source, cancellationToken).ConfigureAwait(false);
        await audit.MailCredentialAsync(action, subject, credential, now, cancellationToken).ConfigureAwait(false);
        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
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
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        string? language = RecipientLanguage.Of(settled, requested: null, languages);
        int told = 0;

        foreach (HeldIdentifier identifier in held.NoticeSet)
        {
            var request = new OutboundMessage(
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
                .UndertakeAsync(request, cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
    }
}
