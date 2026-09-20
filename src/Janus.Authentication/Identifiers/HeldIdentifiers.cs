using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Identifiers;

/// <summary>
/// One account's identifiers as they stand, with the two things only the set can
/// answer: which kinds stand where, and who a security notice reaches.
/// </summary>
/// <param name="All">Every identifier the account holds, verified or not.</param>
/// <param name="Backups">The backup setting of each kind the account has settled.</param>
/// <param name="NoticeSet">
/// Who a security notice reaches, which the backup settings decide and nothing here
/// works out again.
/// </param>
/// <remarks>Implements REG-IDENT-002 and CONV-LAYOUT-001.</remarks>
internal sealed record HeldIdentifiers(
    IReadOnlyList<HeldIdentifier> All,
    IReadOnlyList<HeldBackup> Backups,
    IReadOnlyList<HeldIdentifier> NoticeSet)
{
    /// <summary>
    /// The identifiers of one kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The identifiers of that kind.</returns>
    public IReadOnlyList<HeldIdentifier> OfKind(IdentifierKind kind)
    {
        var held = new List<HeldIdentifier>();

        foreach (HeldIdentifier identifier in All)
        {
            if (identifier.Kind == kind)
            {
                held.Add(identifier);
            }
        }

        return held;
    }

    /// <summary>
    /// One identifier of the account, where it holds it.
    /// </summary>
    /// <param name="id">Which identifier.</param>
    /// <returns>The identifier, or nothing where the account holds no such one.</returns>
    public HeldIdentifier? Find(IdentifierId id)
    {
        foreach (HeldIdentifier identifier in All)
        {
            if (identifier.Id == id)
            {
                return identifier;
            }
        }

        return null;
    }

    /// <summary>
    /// How many verified identifiers of a kind the account holds.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>How many.</returns>
    public int Verified(IdentifierKind kind)
    {
        int counted = 0;

        foreach (HeldIdentifier identifier in All)
        {
            if (identifier.Kind == kind && identifier.IsVerified)
            {
                counted++;
            }
        }

        return counted;
    }

    /// <summary>
    /// The setting one kind stands at, which is the default where the account has
    /// never changed it.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The setting.</returns>
    public HeldBackup Backup(IdentifierKind kind)
    {
        foreach (HeldBackup backup in Backups)
        {
            if (backup.Kind == kind)
            {
                return backup;
            }
        }

        return new HeldBackup(kind, BackupChoice.AllVerified, Named: null);
    }

    /// <summary>
    /// Who a security notice reaches once one identifier is left out of it, which is
    /// what a removal notifies (REG-IDENT-006).
    /// </summary>
    /// <param name="left">The identifier to leave out.</param>
    /// <returns>The rest of the security-notice set.</returns>
    public IReadOnlyList<HeldIdentifier> NoticeSetWithout(IdentifierId left)
    {
        var reached = new List<HeldIdentifier>(NoticeSet.Count);

        foreach (HeldIdentifier identifier in NoticeSet)
        {
            if (identifier.Id != left)
            {
                reached.Add(identifier);
            }
        }

        return reached;
    }
}
