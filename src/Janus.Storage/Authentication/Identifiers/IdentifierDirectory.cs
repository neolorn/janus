using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Core;
using Janus.Identity.Identifiers;
using Janus.Identity.Preferences;

namespace Janus.Storage.Authentication.Identifiers;

/// <summary>
/// What the identifier operations ask of the account directory, over the identity
/// stores.
/// </summary>
/// <param name="identifiers">Where the account's identifiers are read and written.</param>
/// <param name="preferences">Where the account's language is read.</param>
/// <remarks>
/// Implements REG-IDENT-002 to REG-IDENT-009 and CONV-LAYOUT-001. Every rule that
/// holds across a set lives in the set, so each command here reads the set, tells it
/// what happened and hands it back.
/// </remarks>
internal sealed class IdentifierDirectory(
    IIdentifierStore identifiers,
    IPreferenceStore preferences) : IIdentifierDirectory
{
    /// <inheritdoc/>
    public ValueTask<SubjectId?> OwnerAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken) =>
        identifiers.FindOwnerAsync(kind, canonical, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<(SubjectId Subject, IdentifierId Identifier)?> HolderAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken) =>
        identifiers.FindHolderAsync(kind, canonical, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<bool> IsReservedAsync(
        IdentifierKind kind,
        string canonical,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        identifiers.IsReservedAsync(kind, canonical, now, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<bool> IsHeldAsync(
        string canonical,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        identifiers.IsHeldAsync(canonical, now, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<HeldIdentifiers> HeldAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        var all = new List<HeldIdentifier>(set.All.Count);

        foreach (Identifier identifier in set.All)
        {
            all.Add(Held(identifier));
        }

        var backups = new List<HeldBackup>();

        foreach (BackupSetting setting in set.Backups)
        {
            backups.Add(new HeldBackup(setting.Kind, setting.Rule, setting.Named));
        }

        var notified = new List<HeldIdentifier>();

        foreach (Identifier identifier in set.SecurityNoticeSet())
        {
            notified.Add(Held(identifier));
        }

        return new HeldIdentifiers(all, backups, notified);
    }

    /// <inheritdoc/>
    public async ValueTask TakeOnAsync(
        SubjectId subject,
        IdentifierId id,
        IdentifierKind kind,
        string entered,
        string canonical,
        DateTimeOffset at,
        int maximum,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(canonical);

        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        set.Add(Taken(subject, id, kind, entered, canonical, at), maximum);

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask TakeUsernameAsync(
        SubjectId subject,
        IdentifierId id,
        Username username,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        set.Add(Identifier.Username(id, subject, username, at), maximum: 1);

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The corporate address is not an email address.</exception>
    public async ValueTask TakeCorporateAsync(
        SubjectId subject,
        IdentifierId id,
        string entered,
        string canonical,
        IdentifierId personal,
        DateTimeOffset at,
        int maximum,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entered);

        if (!EmailAddress.TryParse(canonical, out EmailAddress address))
        {
            throw new InvalidOperationException("The corporate address is not an email address.");
        }

        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        // The organization asserts the address and the system created its mailbox, so
        // it is proved by the invitation and not by a code (REG-MAIL-001).
        set.Add(Identifier.Email(id, subject, address, entered, at, isLocked: true), maximum);
        set.Verify(id, at);
        set.MakePrimary(id);
        set.KeepPersonal(personal);

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ProveAsync(
        SubjectId subject,
        IdentifierId id,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        set.Verify(id, at);

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SwapAsync(
        SubjectId subject,
        IdentifierId id,
        string entered,
        string canonical,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(canonical);

        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        Required(set, id).Replace(entered, canonical, at);

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ReplaceAsync(
        SubjectId subject,
        IdentifierId id,
        string entered,
        string canonical,
        DateTimeOffset at,
        DateTimeOffset expiresAt,
        byte[] undo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(canonical);
        ArgumentNullException.ThrowIfNull(undo);

        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        Identifier displaced = Required(set, id);

        // The removal is taken before the value moves, because what the undo puts back
        // is the value the row carried and not the row itself: a replace keeps the
        // identity and the role, so nothing that names the identifier is disturbed.
        var removal = IdentifierRemoval.Of(displaced, at, expiresAt, undo);

        displaced.Replace(entered, canonical, at);

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);

        await identifiers.RecordRemovalAsync(removal, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask PromoteAsync(
        SubjectId subject,
        IdentifierId id,
        CancellationToken cancellationToken)
    {
        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        set.MakePrimary(id);

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SettleBackupAsync(
        SubjectId subject,
        IdentifierKind kind,
        BackupChoice choice,
        IdentifierId? named,
        CancellationToken cancellationToken)
    {
        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        BackupSetting setting = set.Backup(kind);

        switch (choice)
        {
            case BackupChoice.PrimaryOnly:
                setting.UsePrimaryOnly();

                break;

            case BackupChoice.Named:
                setting.UseNamed(
                    named ?? throw new ArgumentNullException(
                        nameof(named),
                        "A setting that names an identifier names one."));

                break;

            default:
                setting.UseEveryVerified();

                break;
        }

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DiscardAsync(
        SubjectId subject,
        IdentifierId id,
        CancellationToken cancellationToken)
    {
        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        _ = set.Remove(id);

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<string?> LanguageAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        (await preferences.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false))
            .Language;

    /// <inheritdoc/>
    public async ValueTask GiveUpAsync(
        SubjectId subject,
        IdentifierId id,
        DateTimeOffset at,
        DateTimeOffset expiresAt,
        byte[] undo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(undo);

        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        Identifier given = set.Remove(id);

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);

        await identifiers
            .RecordRemovalAsync(IdentifierRemoval.Of(given, at, expiresAt, undo), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<GivenUpIdentifier?> GivenUpAsync(
        byte[] undo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(undo);

        IdentifierRemoval? removal = await identifiers.FindRemovalAsync(undo, cancellationToken)
            .ConfigureAwait(false);

        return removal is null
            ? null
            : new GivenUpIdentifier(
                removal.Id,
                removal.Subject,
                removal.Kind,
                removal.Entered,
                removal.Canonical,
                removal.ExpiresAt);
    }

    /// <inheritdoc/>
    public async ValueTask TakeBackAsync(
        IdentifierId id,
        int maximum,
        CancellationToken cancellationToken)
    {
        IdentifierRemoval removal = await identifiers.FindRemovalAsync(id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The identifier was not given up.");

        IdentifierSet set = await identifiers
            .FindBySubjectAsync(removal.Subject, cancellationToken)
            .ConfigureAwait(false);

        // A replace left the row standing under its new value, so the undo moves the
        // old value back onto it; a removal took the row away, so the undo adds it.
        if (set.Find(id) is Identifier standing)
        {
            standing.Replace(removal.Entered, removal.Canonical, removal.VerifiedAt);
        }
        else
        {
            set.Add(removal.Restored(), maximum);
        }

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);

        await identifiers.DiscardRemovalAsync(id, cancellationToken).ConfigureAwait(false);
    }

    private static HeldIdentifier Held(Identifier identifier) =>
        new(
            identifier.Id,
            identifier.Kind,
            identifier.Entered,
            identifier.Canonical,
            identifier.IsVerified,
            identifier.IsPrimary,
            identifier.IsLocked,
            identifier.IsPersonal,
            identifier.VerifiedAt);

    private static Identifier Required(IdentifierSet set, IdentifierId id) =>
        set.Find(id) ?? throw new InvalidOperationException("The account holds no such identifier.");

    // A value the operation canonicalised is canonical, so a form that no longer
    // parses is a corrupted command and not a value to take on quietly.
    private static Identifier Taken(
        SubjectId subject,
        IdentifierId id,
        IdentifierKind kind,
        string entered,
        string canonical,
        DateTimeOffset at) =>
        kind is IdentifierKind.Email
            ? Identifier.Email(id, subject, Address(canonical), entered, at)
            : Identifier.Phone(id, subject, Number(canonical), entered, at);

    private static EmailAddress Address(string canonical) =>
        EmailAddress.TryParse(canonical, out EmailAddress address)
            ? address
            : throw new InvalidOperationException("The value is not an address.");

    private static PhoneNumber Number(string canonical) =>
        PhoneNumber.TryParse(canonical, out PhoneNumber number)
            ? number
            : throw new InvalidOperationException("The value is not a number.");
}
