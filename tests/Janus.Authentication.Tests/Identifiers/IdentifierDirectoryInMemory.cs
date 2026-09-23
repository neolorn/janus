using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Core;

namespace Janus.Authentication.Tests.Identifiers;

/// <summary>
/// The identifiers of every account, held in memory under the rules the set of
/// IDN-ACCT-007 keeps: one primary per kind, a backup setting per kind, and a
/// removal that holds the value against every other account until its undo expires.
/// </summary>
internal sealed class IdentifierDirectoryInMemory : IIdentifierDirectory
{
    private readonly Dictionary<SubjectId, List<HeldIdentifier>> _held = [];
    private readonly Dictionary<(SubjectId Subject, IdentifierKind Kind), HeldBackup> _backups = [];
    private readonly Dictionary<IdentifierId, GivenUpIdentifier> _givenUp = [];
    private readonly Dictionary<IdentifierId, byte[]> _undo = [];
    private readonly Dictionary<IdentifierId, DateTimeOffset> _proved = [];
    private readonly Dictionary<string, DateTimeOffset> _usernames = new(StringComparer.Ordinal);
    private readonly Dictionary<SubjectId, string> _languages = [];

    /// <summary>
    /// The language the account reads in, where a test has set one.
    /// </summary>
    /// <param name="subject">Whose language.</param>
    /// <param name="language">The tag.</param>
    public void Reads(SubjectId subject, string language) => _languages[subject] = language;

    /// <summary>
    /// Holds a username against every account, as erasure does.
    /// </summary>
    /// <param name="username">The value held.</param>
    /// <param name="until">When it is released.</param>
    public void Holds(string username, DateTimeOffset until) => _usernames[username] = until;

    /// <summary>
    /// Puts a verified identifier on an account, which is what registration left.
    /// </summary>
    /// <param name="subject">Whose identifier.</param>
    /// <param name="kind">Which kind.</param>
    /// <param name="canonical">Its canonical form.</param>
    /// <param name="isLocked">Whether it is locked against change.</param>
    /// <param name="isPersonal">Whether it is the personal email a membership keeps.</param>
    /// <returns>What it answers to.</returns>
    public IdentifierId Verified(
        SubjectId subject,
        IdentifierKind kind,
        string canonical,
        bool isLocked = false,
        bool isPersonal = false)
    {
        var id = new IdentifierId(Guid.NewGuid());

        Of(subject).Add(new HeldIdentifier(
            id,
            kind,
            canonical,
            canonical,
            IsVerified: true,
            IsPrimary: false,
            isLocked,
            isPersonal,
            DateTimeOffset.UnixEpoch));

        Settle(subject, id);

        return id;
    }

    /// <inheritdoc/>
    public ValueTask<(SubjectId Subject, IdentifierId Identifier)?> HolderAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<SubjectId, List<HeldIdentifier>> account in _held)
        {
            if (account.Value.FirstOrDefault(identifier =>
                    identifier.Kind == kind
                    && string.Equals(identifier.Canonical, canonical, StringComparison.Ordinal))
                is HeldIdentifier held)
            {
                return ValueTask.FromResult<(SubjectId, IdentifierId)?>((account.Key, held.Id));
            }
        }

        return ValueTask.FromResult<(SubjectId, IdentifierId)?>(null);
    }

    /// <inheritdoc/>
    public ValueTask<SubjectId?> OwnerAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<SubjectId, List<HeldIdentifier>> account in _held)
        {
            if (account.Value.Any(identifier =>
                identifier.Kind == kind
                && string.Equals(identifier.Canonical, canonical, StringComparison.Ordinal)))
            {
                return ValueTask.FromResult<SubjectId?>(account.Key);
            }
        }

        return ValueTask.FromResult<SubjectId?>(null);
    }

    /// <inheritdoc/>
    public ValueTask<bool> IsReservedAsync(
        IdentifierKind kind,
        string canonical,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_givenUp.Values.Any(given =>
            given.Kind == kind
            && string.Equals(given.Canonical, canonical, StringComparison.Ordinal)
            && given.ExpiresAt > now));

    /// <inheritdoc/>
    public ValueTask<bool> IsHeldAsync(
        string canonical,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _usernames.TryGetValue(canonical, out DateTimeOffset until) && until > now);

    /// <inheritdoc/>
    public ValueTask<HeldIdentifiers> HeldAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        List<HeldIdentifier> all = Of(subject);

        return ValueTask.FromResult(new HeldIdentifiers(
            [.. all],
            [.. all.Select(identifier => identifier.Kind).Distinct().Select(kind => Backup(subject, kind))],
            [.. all.Where(identifier => Admits(subject, identifier))]));
    }

    /// <inheritdoc/>
    public ValueTask<string?> LanguageAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_languages.GetValueOrDefault(subject));

    /// <inheritdoc/>
    public ValueTask TakeOnAsync(
        SubjectId subject,
        IdentifierId id,
        IdentifierKind kind,
        string entered,
        string canonical,
        DateTimeOffset at,
        int maximum,
        CancellationToken cancellationToken)
    {
        List<HeldIdentifier> all = Of(subject);

        if (all.Count(identifier => identifier.Kind == kind) >= maximum)
        {
            throw new InvalidOperationException("The account holds as many of that kind as it may.");
        }

        all.Add(new HeldIdentifier(
            id,
            kind,
            entered,
            canonical,
            IsVerified: false,
            IsPrimary: false,
            IsLocked: false,
            IsPersonal: false,
            VerifiedAt: null));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask TakeUsernameAsync(
        SubjectId subject,
        IdentifierId id,
        Username username,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        List<HeldIdentifier> all = Of(subject);

        all.RemoveAll(identifier => identifier.Kind is IdentifierKind.Username);
        all.Add(new HeldIdentifier(
            id,
            IdentifierKind.Username,
            username.Value,
            username.Value,
            IsVerified: true,
            IsPrimary: false,
            IsLocked: false,
            IsPersonal: false,
            at));

        Settle(subject, id);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ProveAsync(
        SubjectId subject,
        IdentifierId id,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Replace(subject, id, identifier => identifier with { IsVerified = true, VerifiedAt = at });
        Settle(subject, id);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SwapAsync(
        SubjectId subject,
        IdentifierId id,
        string entered,
        string canonical,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Replace(
            subject,
            id,
            identifier => identifier with { Entered = entered, Canonical = canonical, VerifiedAt = at });

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ReplaceAsync(
        SubjectId subject,
        IdentifierId id,
        string entered,
        string canonical,
        DateTimeOffset at,
        DateTimeOffset expiresAt,
        byte[] undo,
        CancellationToken cancellationToken)
    {
        HeldIdentifier displaced = Required(subject, id);

        _givenUp[id] = new GivenUpIdentifier(
            id,
            subject,
            displaced.Kind,
            displaced.Entered,
            displaced.Canonical,
            expiresAt);
        _undo[id] = undo;
        _proved[id] = displaced.VerifiedAt ?? at;

        Replace(
            subject,
            id,
            identifier => identifier with { Entered = entered, Canonical = canonical, VerifiedAt = at });

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask PromoteAsync(SubjectId subject, IdentifierId id, CancellationToken cancellationToken)
    {
        IdentifierKind kind = Required(subject, id).Kind;
        List<HeldIdentifier> all = Of(subject);

        for (int at = 0; at < all.Count; at++)
        {
            if (all[at].Kind == kind)
            {
                all[at] = all[at] with { IsPrimary = all[at].Id == id };
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SettleBackupAsync(
        SubjectId subject,
        IdentifierKind kind,
        BackupChoice choice,
        IdentifierId? named,
        CancellationToken cancellationToken)
    {
        _backups[(subject, kind)] = new HeldBackup(
            kind,
            choice,
            choice is BackupChoice.Named ? named : null);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DiscardAsync(SubjectId subject, IdentifierId id, CancellationToken cancellationToken)
    {
        _ = Of(subject).RemoveAll(identifier => identifier.Id == id);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask GiveUpAsync(
        SubjectId subject,
        IdentifierId id,
        DateTimeOffset at,
        DateTimeOffset expiresAt,
        byte[] undo,
        CancellationToken cancellationToken)
    {
        HeldIdentifier given = Required(subject, id);

        if (given.IsPrimary)
        {
            throw new InvalidOperationException("The primary of a kind is not removed.");
        }

        _ = Of(subject).RemoveAll(identifier => identifier.Id == id);

        _givenUp[id] = new GivenUpIdentifier(
            id,
            subject,
            given.Kind,
            given.Entered,
            given.Canonical,
            expiresAt);
        _undo[id] = undo;
        _proved[id] = given.VerifiedAt ?? at;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<GivenUpIdentifier?> GivenUpAsync(byte[] undo, CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<IdentifierId, byte[]> held in _undo)
        {
            if (held.Value.SequenceEqual(undo))
            {
                return ValueTask.FromResult<GivenUpIdentifier?>(_givenUp[held.Key]);
            }
        }

        return ValueTask.FromResult<GivenUpIdentifier?>(null);
    }

    /// <inheritdoc/>
    public ValueTask TakeBackAsync(IdentifierId id, int maximum, CancellationToken cancellationToken)
    {
        GivenUpIdentifier given = _givenUp[id];
        List<HeldIdentifier> all = Of(given.Subject);

        if (all.Any(identifier => identifier.Id == id))
        {
            Replace(
                given.Subject,
                id,
                identifier => identifier with
                {
                    Entered = given.Entered,
                    Canonical = given.Canonical,
                    VerifiedAt = _proved[id],
                });
        }
        else
        {
            if (all.Count(identifier => identifier.Kind == given.Kind) >= maximum)
            {
                throw new InvalidOperationException("The account holds as many of that kind as it may.");
            }

            all.Add(new HeldIdentifier(
                id,
                given.Kind,
                given.Entered,
                given.Canonical,
                IsVerified: true,
                IsPrimary: false,
                IsLocked: false,
                IsPersonal: false,
                _proved[id]));

            Settle(given.Subject, id);
        }

        _ = _givenUp.Remove(id);
        _ = _undo.Remove(id);
        _ = _proved.Remove(id);

        return ValueTask.CompletedTask;
    }

    private HeldBackup Backup(SubjectId subject, IdentifierKind kind) =>
        _backups.GetValueOrDefault(
            (subject, kind),
            new HeldBackup(kind, BackupChoice.AllVerified, Named: null));

    private bool Admits(SubjectId subject, HeldIdentifier identifier)
    {
        if (!identifier.IsVerified || identifier.Kind is IdentifierKind.Username)
        {
            return false;
        }

        if (identifier.IsPersonal)
        {
            return true;
        }

        HeldBackup setting = Backup(subject, identifier.Kind);

        return setting.Choice switch
        {
            BackupChoice.AllVerified => true,
            BackupChoice.Named => identifier.IsPrimary || identifier.Id == setting.Named,
            _ => identifier.IsPrimary,
        };
    }

    private List<HeldIdentifier> Of(SubjectId subject)
    {
        if (!_held.TryGetValue(subject, out List<HeldIdentifier>? all))
        {
            all = [];
            _held[subject] = all;
        }

        return all;
    }

    private HeldIdentifier Required(SubjectId subject, IdentifierId id) =>
        Of(subject).SingleOrDefault(identifier => identifier.Id == id)
        ?? throw new InvalidOperationException("The account holds no such identifier.");

    private void Replace(
        SubjectId subject,
        IdentifierId id,
        Func<HeldIdentifier, HeldIdentifier> changed)
    {
        List<HeldIdentifier> all = Of(subject);

        for (int at = 0; at < all.Count; at++)
        {
            if (all[at].Id == id)
            {
                all[at] = changed(all[at]);
            }
        }
    }

    // One primary per kind, taken by the first verified identifier of that kind.
    private void Settle(SubjectId subject, IdentifierId id)
    {
        HeldIdentifier taken = Required(subject, id);

        if (taken.IsVerified
            && !Of(subject).Any(identifier => identifier.Kind == taken.Kind && identifier.IsPrimary))
        {
            Replace(subject, id, identifier => identifier with { IsPrimary = true });
        }
    }
}
