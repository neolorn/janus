using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Privacy.Erasures;

/// <summary>
/// The lengths of the two grace windows an account's deletion runs under, and the
/// instant each deletion's erasure falls due.
/// </summary>
/// <param name="Deletion">The length of <c>account.deletion.grace</c>.</param>
/// <param name="Takedown">The length of <c>takedown.grace</c>.</param>
/// <remarks>
/// Implements IDN-LIFE-003, IDN-LIFE-014 and IDN-ACCT-007. The instant is computed here
/// and nowhere else, so the sweep that erases, the reversal that is refused at the
/// window's end and the progress an administrator reads agree on it.
/// </remarks>
internal sealed record DeletionWindows(TimeSpan Deletion, TimeSpan Takedown)
{
    /// <summary>
    /// The shorter of the two windows, which bounds how long ago any window that has
    /// run out began.
    /// </summary>
    public TimeSpan Shortest => Deletion < Takedown ? Deletion : Takedown;

    /// <summary>
    /// Reads both lengths.
    /// </summary>
    /// <param name="configuration">Where they are held.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The two windows.</returns>
    /// <exception cref="InvalidOperationException">
    /// A stored length does not read, which is a fault and never a default (CONV-ERR-001).
    /// </exception>
    public static async ValueTask<DeletionWindows> ReadAsync(
        IConfigurationStore configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        TimeSpan deletion = await ReadAsync(configuration, Settings.AccountDeletionGrace, cancellationToken)
            .ConfigureAwait(false);
        TimeSpan takedown = await ReadAsync(configuration, Settings.TakedownGrace, cancellationToken)
            .ConfigureAwait(false);

        return new DeletionWindows(deletion, takedown);
    }

    /// <summary>
    /// When a deletion's erasure falls due.
    /// </summary>
    /// <param name="by">What began the window.</param>
    /// <param name="since">When the window began.</param>
    /// <param name="heldSince">
    /// When the deletion a takedown found running began, where it found one.
    /// </param>
    /// <returns>The instant.</returns>
    /// <remarks>
    /// A takedown borrows the deletion timer and not its length, and never erases later
    /// than the subject's own deletion would have: where it holds one, the erasure is
    /// due at the earlier of that window's end and its own.
    /// </remarks>
    public DateTimeOffset ErasureDue(DeletionOrigin by, DateTimeOffset since, DateTimeOffset? heldSince)
    {
        if (by is not DeletionOrigin.Takedown)
        {
            return since + Deletion;
        }

        DateTimeOffset own = since + Takedown;

        return heldSince is DateTimeOffset held && held + Deletion < own ? held + Deletion : own;
    }

    private static async ValueTask<TimeSpan> ReadAsync(
        IConfigurationStore configuration,
        DurationSetting setting,
        CancellationToken cancellationToken) =>
        (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));
}
