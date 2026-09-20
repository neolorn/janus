using System;
using Janus.Core;

namespace Janus.Identity.Identifiers;

/// <summary>
/// One kind's backup setting, which decides what the security-notice set holds beyond
/// the primary.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-002. The setting exists once per account and kind; where the
/// account has never changed it, it is <see cref="BackupChoice.AllVerified"/> and no row
/// is written.
/// </remarks>
internal sealed class BackupSetting
{
    private BackupSetting(SubjectId subject, IdentifierKind kind, BackupChoice rule, IdentifierId? named)
    {
        Subject = subject;
        Kind = kind;
        Rule = rule;
        Named = named;
    }

    /// <summary>
    /// Whose setting it is.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// The kind it governs.
    /// </summary>
    public IdentifierKind Kind { get; }

    /// <summary>
    /// What it adds to the primary.
    /// </summary>
    public BackupChoice Rule { get; private set; }

    /// <summary>
    /// The one identifier it names, where it names one.
    /// </summary>
    public IdentifierId? Named { get; private set; }

    /// <summary>
    /// Records a setting for a kind the account has changed it for.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="kind">The kind it governs.</param>
    /// <returns>The setting, at its default.</returns>
    public static BackupSetting Default(SubjectId subject, IdentifierKind kind) =>
        new(subject, kind, BackupChoice.AllVerified, named: null);

    /// <summary>
    /// The setting as it already stands. This is the store translating a stored row and
    /// no change the account made.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="kind">The kind it governs.</param>
    /// <param name="rule">What it adds to the primary.</param>
    /// <param name="named">The identifier it names, where it names one.</param>
    /// <returns>The setting.</returns>
    public static BackupSetting Existing(
        SubjectId subject,
        IdentifierKind kind,
        BackupChoice rule,
        IdentifierId? named) =>
        new(subject, kind, rule, named);

    /// <summary>
    /// Sets the security-notice set to every verified identifier of the kind.
    /// </summary>
    public void UseEveryVerified()
    {
        Rule = BackupChoice.AllVerified;
        Named = null;
    }

    /// <summary>
    /// Sets the security-notice set to the primary alone.
    /// </summary>
    public void UsePrimaryOnly()
    {
        Rule = BackupChoice.PrimaryOnly;
        Named = null;
    }

    /// <summary>
    /// Sets the security-notice set to the primary and one named identifier.
    /// </summary>
    /// <param name="named">The identifier to name.</param>
    public void UseNamed(IdentifierId named)
    {
        Rule = BackupChoice.Named;
        Named = named;
    }

    /// <summary>
    /// Whether an identifier is in the security-notice set under this setting.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>Whether a security notice reaches it.</returns>
    /// <exception cref="ArgumentNullException">The identifier is absent.</exception>
    public bool Admits(Identifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        if (!identifier.IsVerified)
        {
            return false;
        }

        return Rule switch
        {
            BackupChoice.AllVerified => true,
            BackupChoice.Named => identifier.IsPrimary || identifier.Id == Named,
            _ => identifier.IsPrimary,
        };
    }
}
