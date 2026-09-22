using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Accounts;

/// <summary>
/// What an account reads and edits about itself.
/// </summary>
/// <param name="lifecycle">What the account does to its own standing.</param>
/// <param name="directory">Where the standing, the profile and the preferences are.</param>
/// <param name="identifiers">Where the account's identifiers are.</param>
/// <param name="authenticators">Where the account's credentials are.</param>
/// <param name="recoveryCodes">Where the account's single-use codes are.</param>
/// <param name="audit">Where a change the account made to itself is recorded.</param>
/// <param name="stepUp">What a username change asks of the session.</param>
/// <param name="reserved">The usernames no account takes.</param>
/// <param name="declarations">The preference keys the host declared.</param>
/// <param name="configuration">Where the settings that switch fields on are read.</param>
/// <param name="work">The transaction the whole of one operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, REG-ACCT-001, REG-PROF-001, REG-PREF-001, REG-IDENT-009 and
/// IDN-ATTR-008. A field whose key is off is neither accepted nor returned, so the
/// deployment and not the caller decides the shape of an answer.
/// </remarks>
internal sealed class AccountService(
    AccountLifecycle lifecycle,
    IAccountDirectory directory,
    IIdentifierDirectory identifiers,
    IAuthenticatorStore authenticators,
    IRecoveryCodeStore recoveryCodes,
    IAccountAudit audit,
    StepUpGuard stepUp,
    ReservedUsernames reserved,
    PreferenceDeclarations declarations,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time) : IAccount
{
    private static readonly AuditAction ProfileChanged = AuditActions.ProfileChanged;

    private static readonly AuditAction UsernameChanged = AuditActions.UsernameChanged;

    private static readonly AuditAction PreferencesChanged =
        AuditActions.PreferencesChanged;

    private static readonly AuditAction CredentialLabelled =
        AuditActions.CredentialLabelled;

    private static readonly AuditAction SecondStepPreferred =
        AuditActions.SecondStepPreferred;

    /// <inheritdoc/>
    public async ValueTask<Result<AccountDetail>> ReadAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure<AccountDetail>(Error.From(ErrorCodes.Denied));
        }

        if (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false)
            is not AccountState state)
        {
            return Result.Failure<AccountDetail>(Error.From(ErrorCodes.Denied));
        }

        Error? failure = null;

        Fields fields = (await FieldsAsync(cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<Fields>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<AccountDetail>(failure);
        }

        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        RecoveryCodeSet? codes = await recoveryCodes.FindAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        HeldProfile profile = await directory.ProfileAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        HeldPreferences preferences = await directory.PreferencesAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new AccountDetail(
            state,
            Shown(held, fields),
            Shown(enrolled),
            codes is null
                ? null
                : new RecoveryCodeStatus(
                    codes.Remaining,
                    codes.GeneratedAt,
                    codes.ViewedAt,
                    codes.ExportedAt),
            Shown(profile, fields),
            Shown(preferences)));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> EditProfileAsync(
        AccessContext context,
        SessionId session,
        ProfileEdit edit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(edit);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // The date is the one entered at the age step and is corrected through
        // support; an edit that carries it is refused whether or not it is retained
        // (REG-PROF-001).
        if (edit.DateOfBirth is not null)
        {
            return Result.Failure(Error.From(ErrorCodes.ProfileNotAccepted));
        }

        Error? failure = null;

        Fields fields = (await FieldsAsync(cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<Fields>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if ((edit.LegalName is not null && !fields.LegalName)
            || (edit.Username is not null && !fields.Username))
        {
            return Result.Failure(Error.From(ErrorCodes.ProfileNotAccepted));
        }

        HeldProfile held = await directory.ProfileAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (Read(edit.DisplayName, held.DisplayName, DisplayName.TryParse, out DisplayName? displayName)
            is Error badDisplayName)
        {
            return Result.Failure(badDisplayName);
        }

        if (Read(edit.LegalName, held.LegalName, LegalName.TryParse, out LegalName? legalName)
            is Error badLegalName)
        {
            return Result.Failure(badLegalName);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (edit.Username is string entered
            && await ChooseAsync(context, subject, session, entered, now, cancellationToken)
                .ConfigureAwait(false) is Error refused)
        {
            return Result.Failure(refused);
        }

        await directory.RecordProfileAsync(subject, displayName, legalName, cancellationToken)
            .ConfigureAwait(false);

        await audit
            .RecordedAsync(ProfileChanged, Acting(context, subject), subject, now, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result<PreferenceValues>> ReadPreferencesAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure<PreferenceValues>(Error.From(ErrorCodes.Denied));
        }

        HeldPreferences held = await directory.PreferencesAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(Shown(held));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> SetPreferencesAsync(
        AccessContext context,
        string? language,
        string? timeZone,
        IReadOnlyDictionary<string, string> declared,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(declared);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        Error? failure = null;

        int maximumSize = (await configuration
                .ReadAsync(Settings.PreferencesMaxSize, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        // The account application's own endpoint, where the person is always the
        // caller: what an administrator may set is set from the management
        // application, which is its own operation (09 section 6, REG-PREF-001).
        Result recorded = await directory
            .RecordPreferencesAsync(
                subject,
                language,
                timeZone,
                declared,
                asAdministrator: false,
                maximumSize,
                cancellationToken)
            .ConfigureAwait(false);

        recorded.Switch(() => { }, error => failure = error);

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        await audit
            .RecordedAsync(PreferencesChanged, Acting(context, subject), subject, now, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<CredentialSummary>>> ListCredentialsAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure<IReadOnlyList<CredentialSummary>>(Error.From(ErrorCodes.Denied));
        }

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<CredentialSummary>>(Shown(enrolled));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> LabelCredentialAsync(
        AccessContext context,
        AuthenticatorId credential,
        string label,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(label);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (!CredentialLabel.TryParse(label, out CredentialLabel named))
        {
            return Result.Failure(Error.From(ErrorCodes.CredentialLabelInvalid));
        }

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        Authenticator? held = null;

        foreach (Authenticator candidate in enrolled)
        {
            if (candidate.Id == credential)
            {
                held = candidate;
            }
        }

        if (held is null)
        {
            return Result.Failure(Error.From(ErrorCodes.CredentialNotFound));
        }

        // AUTH-FACT-001 AC5: a label is held once per kind per account. The database
        // holds it too, but a refusal the person can read beats a failed commit.
        foreach (Authenticator candidate in enrolled)
        {
            if (candidate.Id != credential
                && candidate.Factor == held.Factor
                && candidate.Label == named)
            {
                return Result.Failure(Error.From(ErrorCodes.CredentialLabelInvalid));
            }
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        held.Rename(named);

        await authenticators.RecordAsync(held, cancellationToken).ConfigureAwait(false);

        await audit
            .RecordedAsync(CredentialLabelled, Acting(context, subject), subject, now, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> PreferSecondStepAsync(
        AccessContext context,
        AuthenticatorId credential,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        Authenticator? chosen = null;

        foreach (Authenticator held in enrolled)
        {
            if (held.Id == credential && SecondStep.Is(held.Factor))
            {
                chosen = held;
            }
        }

        // IDN-ATTR-008 AC2: the preference names something the account holds, and a
        // credential that is not a second step is not one of them.
        if (chosen is null || chosen.State is not AuthenticatorState.Active)
        {
            return Result.Failure(Error.From(ErrorCodes.CredentialNotFound));
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        // At most one credential carries the mark, which the database holds as well.
        foreach (Authenticator held in enrolled)
        {
            if (held.IsPreferred && held.Id != credential)
            {
                held.Prefer(false);

                await authenticators.RecordAsync(held, cancellationToken).ConfigureAwait(false);
            }
        }

        chosen.Prefer(true);

        await authenticators.RecordAsync(chosen, cancellationToken).ConfigureAwait(false);

        await audit
            .RecordedAsync(SecondStepPreferred, Acting(context, subject), subject, now, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public ValueTask<Result> DeactivateAsync(
        AccessContext context,
        SessionId session,
        string source,
        CancellationToken cancellationToken) =>
        lifecycle.DeactivateAsync(context, session, source, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> ReactivateAsync(string linkToken, CancellationToken cancellationToken) =>
        lifecycle.ReactivateAsync(linkToken, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<DateTimeOffset>> DeleteAsync(
        AccessContext context,
        SessionId session,
        string source,
        CancellationToken cancellationToken) =>
        lifecycle.DeleteAsync(context, session, source, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> CancelDeletionAsync(
        string linkToken,
        CancellationToken cancellationToken) =>
        lifecycle.CancelDeletionAsync(linkToken, cancellationToken);

    private static SubjectId Acting(AccessContext context, SubjectId subject) =>
        context.Acting ?? subject;

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // A field the edit left out is left as it stands; one present and empty is given
    // up; one present and filled in is read (REG-PROF-001). IDN-ACCT-005 answers its
    // own code, because the endpoint names it and nothing else the profile refuses
    // tells the person what to change.
    private static Error? Read<TName>(
        string? entered,
        TName? held,
        Parser<TName> parse,
        out TName? name)
        where TName : struct
    {
        name = held;

        if (entered is null)
        {
            return null;
        }

        if (entered.Length is 0)
        {
            name = null;

            return null;
        }

        if (!ScriptMixing.IsSingleScriptPerWord(entered))
        {
            return Error.From(ErrorCodes.IdentifierMixedScript);
        }

        if (!parse(entered, out TName read))
        {
            return Error.From(ErrorCodes.ProfileInvalid);
        }

        name = read;

        return null;
    }

    private static List<CredentialSummary> Shown(IReadOnlyList<Authenticator> enrolled)
    {
        Authenticator? preferred = SecondStep.Preferred(enrolled);

        var summaries = new List<CredentialSummary>(enrolled.Count);

        foreach (Authenticator credential in enrolled)
        {
            summaries.Add(new CredentialSummary(
                credential.Id,
                credential.Factor,
                credential.Label.Value,
                credential.State,
                credential.InvalidatesAt,
                credential.WebAuthn?.BackupEligible,
                credential.WebAuthn?.BackupState,
                preferred is not null && preferred.Id == credential.Id,
                credential.AddedAt,
                credential.LastUsedAt));
        }

        return summaries;
    }

    private static AccountIdentifiers Shown(HeldIdentifiers held, Fields fields)
    {
        string? username = null;

        if (fields.Username)
        {
            foreach (HeldIdentifier identifier in held.OfKind(IdentifierKind.Username))
            {
                username = identifier.Entered;
            }
        }

        var backup = new List<BackupSelection>(held.Backups.Count);

        foreach (HeldBackup setting in held.Backups)
        {
            backup.Add(new BackupSelection(setting.Kind, setting.Choice, setting.Named));
        }

        return new AccountIdentifiers(
            Shown(held, IdentifierKind.Email),
            Shown(held, IdentifierKind.Phone),
            username,
            backup);
    }

    private static List<IdentifierSummary> Shown(HeldIdentifiers held, IdentifierKind kind)
    {
        IReadOnlyList<HeldIdentifier> ofKind = held.OfKind(kind);

        var summaries = new List<IdentifierSummary>(ofKind.Count);

        foreach (HeldIdentifier identifier in ofKind)
        {
            summaries.Add(new IdentifierSummary(
                identifier.Id,
                identifier.Entered,
                identifier.IsVerified,
                identifier.IsPrimary,
                identifier.IsLocked));
        }

        return summaries;
    }

    private static ProfileDetail Shown(HeldProfile profile, Fields fields) =>
        new(
            profile.DisplayName?.Value,
            fields.LegalName ? profile.LegalName?.Value : null,
            fields.DateOfBirth ? profile.DateOfBirth : null,
            profile.PhotoUpdatedAt);

    private PreferenceValues Shown(HeldPreferences held)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (PreferenceDeclaration declaration in declarations.All)
        {
            values[declaration.Name] = held.Values.TryGetValue(declaration.Name, out string? set)
                ? set
                : declaration.Default;
        }

        return new PreferenceValues(held.Language, held.TimeZone, values);
    }

    private async ValueTask<Result<Fields>> FieldsAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        AttributeRequirement legalName = (await configuration
                .ReadAsync(Settings.ProfileLegalName, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<AttributeRequirement>(error, ref failure));

        AttributeRequirement dateOfBirth = (await configuration
                .ReadAsync(Settings.ProfileDateOfBirth, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<AttributeRequirement>(error, ref failure));

        bool username = (await configuration
                .ReadAsync(Settings.IdentifiersUsernameEnabled, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<bool>(error, ref failure));

        return failure is not null
            ? Result.Failure<Fields>(failure)
            : Result.Success(new Fields(
                legalName is not AttributeRequirement.Off,
                dateOfBirth is not AttributeRequirement.Off,
                username));
    }

    // REG-IDENT-009: a username is chosen through the profile, is never verified, is
    // public by nature and so discloses its own refusals, and is held against a second
    // change for as long as the cooling off lasts.
    private async ValueTask<Error?> ChooseAsync(
        AccessContext context,
        SubjectId subject,
        SessionId session,
        string entered,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!Username.TryParse(entered, out Username username))
        {
            return Error.From(ErrorCodes.UsernameInvalid);
        }

        if (reserved.Holds(username))
        {
            return Error.From(ErrorCodes.UsernameReserved);
        }

        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        HeldIdentifier? standing = null;

        foreach (HeldIdentifier identifier in held.OfKind(IdentifierKind.Username))
        {
            standing = identifier;
        }

        if (standing is not null && string.Equals(standing.Canonical, username.Value, StringComparison.Ordinal))
        {
            return null;
        }

        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.UsernameChange, cancellationToken)
                .ConfigureAwait(false)
            is Error closed)
        {
            return closed;
        }

        if (standing is not null
            && await CoolingOffAsync(standing, now, cancellationToken).ConfigureAwait(false)
                is Error waiting)
        {
            return waiting;
        }

        if (await identifiers
                .OwnerAsync(IdentifierKind.Username, username.Value, cancellationToken)
                .ConfigureAwait(false) is not null
            || await identifiers.IsHeldAsync(username.Value, now, cancellationToken)
                .ConfigureAwait(false))
        {
            return Error.From(ErrorCodes.UsernameTaken);
        }

        if (standing is null)
        {
            await identifiers
                .TakeUsernameAsync(subject, IdentifierId.New(time), username, now, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await identifiers
                .SwapAsync(subject, standing.Id, username.Value, username.Value, now, cancellationToken)
                .ConfigureAwait(false);
        }

        await audit
            .RecordedAsync(UsernameChanged, Acting(context, subject), subject, now, cancellationToken)
            .ConfigureAwait(false);

        return null;
    }

    private async ValueTask<Error?> CoolingOffAsync(
        HeldIdentifier standing,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.IdentifiersUsernameChangeCoolOff, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        if (standing.VerifiedAt is not DateTimeOffset chosen || chosen + window <= now)
        {
            return null;
        }

        return Error.From(
            ErrorCodes.UsernameCoolingOff,
            "retryAt",
            JsonSerializer.SerializeToElement(chosen + window));
    }

    private delegate bool Parser<TName>(string entered, out TName name)
        where TName : struct;

    // Which of the fields a policy switches this deployment collects, read once so
    // that one answer shapes the whole of one operation.
    private sealed record Fields(bool LegalName, bool DateOfBirth, bool Username);
}
