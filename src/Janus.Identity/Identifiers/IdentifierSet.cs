using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Identity.Identifiers;

/// <summary>
/// One account's identifiers, with the rules that hold across them rather than over any
/// one of them.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-001, REG-IDENT-002 and REG-IDENT-005. Exactly one identifier of
/// a kind is primary whenever the account has a verified one of that kind, so the first
/// to verify takes the role and never leaves it empty. Ordinary communications go to
/// the primary; security notices go to the set the kind's backup setting defines, and
/// to the personal email a membership keeps whatever that setting is (REG-MAIL-001).
/// </remarks>
internal sealed class IdentifierSet
{
    private readonly List<Identifier> _identifiers;
    private readonly Dictionary<IdentifierKind, BackupSetting> _settings;
    private readonly List<IdentifierId> _removed = [];

    private IdentifierSet(
        SubjectId subject,
        List<Identifier> identifiers,
        Dictionary<IdentifierKind, BackupSetting> settings)
    {
        Subject = subject;
        _identifiers = identifiers;
        _settings = settings;
    }

    /// <summary>
    /// Whose identifiers these are.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// Every identifier the account holds, verified or not.
    /// </summary>
    public IReadOnlyList<Identifier> All => _identifiers;

    /// <summary>
    /// The identifiers taken off the account since it was read, which is what a store
    /// gives the rows up for.
    /// </summary>
    public IReadOnlyList<IdentifierId> Removed => _removed;

    /// <summary>
    /// The backup settings the account has a setting for, which is what a store writes;
    /// a kind the account has never settled keeps the default and needs no row.
    /// </summary>
    public IReadOnlyCollection<BackupSetting> Backups => _settings.Values;

    /// <summary>
    /// Reads an account's identifiers as they stand.
    /// </summary>
    /// <param name="subject">Whose they are.</param>
    /// <param name="identifiers">The identifiers held.</param>
    /// <param name="settings">The backup settings the account has changed.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">Either collection is absent.</exception>
    /// <exception cref="ArgumentException">
    /// An identifier or a setting belongs to another account.
    /// </exception>
    public static IdentifierSet Of(
        SubjectId subject,
        IEnumerable<Identifier> identifiers,
        IEnumerable<BackupSetting> settings)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(settings);

        var held = new List<Identifier>();
        var backups = new Dictionary<IdentifierKind, BackupSetting>();

        foreach (Identifier identifier in identifiers)
        {
            if (identifier.Subject != subject)
            {
                throw new ArgumentException("The identifier belongs to another account.", nameof(identifiers));
            }

            held.Add(identifier);
        }

        foreach (BackupSetting setting in settings)
        {
            if (setting.Subject != subject)
            {
                throw new ArgumentException("The setting belongs to another account.", nameof(settings));
            }

            backups[setting.Kind] = setting;
        }

        return new IdentifierSet(subject, held, backups);
    }

    /// <summary>
    /// The identifiers of one kind, in the order they were added.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The identifiers of that kind.</returns>
    public IReadOnlyList<Identifier> OfKind(IdentifierKind kind)
    {
        var held = new List<Identifier>();

        foreach (Identifier identifier in _identifiers)
        {
            if (identifier.Kind == kind)
            {
                held.Add(identifier);
            }
        }

        return held;
    }

    /// <summary>
    /// The primary of a kind, where the account has a verified identifier of it.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The primary, or nothing.</returns>
    public Identifier? Primary(IdentifierKind kind)
    {
        foreach (Identifier identifier in _identifiers)
        {
            if (identifier.Kind == kind && identifier.IsPrimary)
            {
                return identifier;
            }
        }

        return null;
    }

    /// <summary>
    /// The kind's backup setting, at its default where the account has never changed
    /// it.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The setting.</returns>
    public BackupSetting Backup(IdentifierKind kind)
    {
        if (!_settings.TryGetValue(kind, out BackupSetting? setting))
        {
            setting = BackupSetting.Default(Subject, kind);
            _settings[kind] = setting;
        }

        return setting;
    }

    /// <summary>
    /// Who a security notice of a kind reaches: the primary plus whatever the kind's
    /// backup setting adds, and the personal email a membership keeps.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The security-notice set.</returns>
    public IReadOnlyList<Identifier> SecurityNoticeSet(IdentifierKind kind)
    {
        BackupSetting setting = Backup(kind);
        var reached = new List<Identifier>();

        foreach (Identifier identifier in _identifiers)
        {
            if (identifier.Kind == kind && (identifier.IsPersonal || setting.Admits(identifier)))
            {
                reached.Add(identifier);
            }
        }

        return reached;
    }

    /// <summary>
    /// Who a security notice reaches: each kind's set, together. A username is not a
    /// destination, so nothing is addressed to one.
    /// </summary>
    /// <returns>The security-notice set.</returns>
    public IReadOnlyList<Identifier> SecurityNoticeSet()
    {
        var reached = new List<Identifier>();

        foreach (Identifier identifier in _identifiers)
        {
            if (identifier.Kind is not IdentifierKind.Username
                && (identifier.IsPersonal || Backup(identifier.Kind).Admits(identifier)))
            {
                reached.Add(identifier);
            }
        }

        return reached;
    }

    /// <summary>
    /// One of the account's identifiers, where it holds it.
    /// </summary>
    /// <param name="id">Which identifier.</param>
    /// <returns>The identifier, or nothing where the account holds no such one.</returns>
    public Identifier? Find(IdentifierId id)
    {
        foreach (Identifier identifier in _identifiers)
        {
            if (identifier.Id == id)
            {
                return identifier;
            }
        }

        return null;
    }

    /// <summary>
    /// Adds an identifier to the account.
    /// </summary>
    /// <param name="identifier">The identifier to add.</param>
    /// <param name="maximum">How many of its kind the account may hold.</param>
    /// <exception cref="ArgumentNullException">The identifier is absent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is not a maximum.</exception>
    /// <exception cref="ArgumentException">It belongs to another account.</exception>
    /// <exception cref="InvalidOperationException">
    /// The account already holds the value, or holds as many of the kind as it may.
    /// </exception>
    public void Add(Identifier identifier, int maximum)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximum, 1);

        if (identifier.Subject != Subject)
        {
            throw new ArgumentException("The identifier belongs to another account.", nameof(identifier));
        }

        int held = 0;

        foreach (Identifier existing in _identifiers)
        {
            if (existing.Kind != identifier.Kind)
            {
                continue;
            }

            if (string.Equals(existing.Canonical, identifier.Canonical, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The account holds that identifier already.");
            }

            held++;
        }

        if (held >= maximum)
        {
            throw new InvalidOperationException("The account holds as many of that kind as it may.");
        }

        _identifiers.Add(identifier);
        _removed.Remove(identifier.Id);
        TakePrimaryIfVacant(identifier);
    }

    /// <summary>
    /// Records that a code or a same-browser link confirmed an identifier. The first of
    /// its kind to verify becomes the primary, so no kind is left with verified
    /// identifiers and no primary.
    /// </summary>
    /// <param name="id">Which identifier was confirmed.</param>
    /// <param name="at">When it was confirmed.</param>
    /// <exception cref="InvalidOperationException">
    /// The account holds no such identifier, or it is verified already.
    /// </exception>
    public void Verify(IdentifierId id, DateTimeOffset at)
    {
        Identifier identifier = Require(id);

        identifier.Verify(at);
        TakePrimaryIfVacant(identifier);
    }

    /// <summary>
    /// Makes a verified identifier the primary of its kind, displacing the one that
    /// held the role.
    /// </summary>
    /// <param name="id">Which identifier to promote.</param>
    /// <exception cref="InvalidOperationException">
    /// The account holds no such identifier, it is not verified, or it is the personal
    /// email a membership keeps.
    /// </exception>
    public void MakePrimary(IdentifierId id)
    {
        Identifier identifier = Require(id);
        Identifier? displaced = Primary(identifier.Kind);

        identifier.MakePrimary();

        if (displaced is not null && displaced.Id != identifier.Id)
        {
            displaced.Relinquish();
        }
    }

    /// <summary>
    /// Keeps a verified email as the personal email of a membership, which the account
    /// then holds verified and non-primary until that membership ends (REG-MAIL-001).
    /// </summary>
    /// <param name="id">Which email.</param>
    /// <exception cref="InvalidOperationException">
    /// The account holds no such identifier, or it is not a verified email other than
    /// the primary.
    /// </exception>
    public void KeepPersonal(IdentifierId id) => Require(id).KeepAsPersonal();

    /// <summary>
    /// Retires the corporate address a membership gave the account when that membership
    /// ends: the personal email the membership kept becomes the primary email in the
    /// same step and is kept no longer, and the corporate address leaves the account,
    /// free for a later invitation (REG-MAIL-003).
    /// </summary>
    /// <param name="canonical">
    /// The corporate address in its canonical form. Where the account no longer holds
    /// it, the personal email still takes the primary role.
    /// </param>
    /// <returns>The personal email, now the primary.</returns>
    /// <exception cref="InvalidOperationException">No personal email is kept.</exception>
    public IdentifierId RetireCorporate(string canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);

        Identifier personal = _identifiers.Find(identifier => identifier.IsPersonal)
            ?? throw new InvalidOperationException("No personal email is kept for the account to continue on.");
        Identifier? corporate = _identifiers.Find(identifier =>
            identifier.Kind is IdentifierKind.Email
            && string.Equals(identifier.Canonical, canonical, StringComparison.Ordinal));

        personal.Release();
        MakePrimary(personal.Id);

        if (corporate is not null)
        {
            _identifiers.Remove(corporate);
            _removed.Add(corporate.Id);
        }

        return personal.Id;
    }

    /// <summary>
    /// Takes an identifier off the account. The primary of a kind is never removed:
    /// another of its kind takes the role first (REG-IDENT-006). The personal email a
    /// membership keeps is not removed while it keeps it (REG-MAIL-001).
    /// </summary>
    /// <param name="id">Which identifier to remove.</param>
    /// <returns>The identifier as it stood, for the removal record to keep.</returns>
    /// <exception cref="InvalidOperationException">
    /// The account holds no such identifier, or it is the primary of its kind or the
    /// personal email a membership keeps.
    /// </exception>
    public Identifier Remove(IdentifierId id)
    {
        Identifier identifier = Require(id);

        if (identifier.IsPrimary)
        {
            throw new InvalidOperationException("The primary of a kind is not removed.");
        }

        if (identifier.IsPersonal)
        {
            throw new InvalidOperationException("The personal email a membership keeps is not removed.");
        }

        _identifiers.Remove(identifier);
        _removed.Add(identifier.Id);

        return identifier;
    }

    private void TakePrimaryIfVacant(Identifier identifier)
    {
        if (identifier.IsVerified && Primary(identifier.Kind) is null)
        {
            identifier.MakePrimary();
        }
    }

    private Identifier Require(IdentifierId id)
    {
        foreach (Identifier identifier in _identifiers)
        {
            if (identifier.Id == id)
            {
                return identifier;
            }
        }

        throw new InvalidOperationException("The account holds no such identifier.");
    }
}
