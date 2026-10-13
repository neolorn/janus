using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Configuration;

/// <summary>
/// Where the value in force for a runtime key is written, which
/// <see cref="ConfigurationAdministration"/> alone reaches.
/// </summary>
/// <remarks>
/// Implements OPS-CFG-008, OPS-CFG-005 and OPS-CFG-004. The store the host reads is
/// read-only; a write that went round the one writer would be a change with no step-up,
/// no reason and no record, so the write is a port of this assembly and no host holds
/// it.
/// </remarks>
internal interface IConfigurationWrites
{
    /// <summary>
    /// Takes a key's row under a lock held to the end of the operation's transaction,
    /// so the value in force read after it is the committed one and a concurrent change
    /// of the key waits for this one.
    /// </summary>
    /// <param name="key">The key, or the member of a family.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The work of taking it.</returns>
    /// <remarks>
    /// Implements X3 of D-166 for runtime settings (178) and OPS-CFG-002 AC6. A key with
    /// no row takes no lock; its first change inserts the row, and a concurrent first
    /// change whose insert then fails on the key is a fault.
    /// </remarks>
    /// <exception cref="System.InvalidOperationException">No transaction is running.</exception>
    ValueTask HoldAsync(ConfigurationKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the value in force for a key the application may change, so the next
    /// read of it anywhere in the deployment sees the change without a restart.
    /// </summary>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <param name="setting">The setting, from <see cref="Settings"/>.</param>
    /// <param name="value">The value to put in force.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// What was in force before the write, or the failure where the key is one the
    /// application cannot change (OPS-CFG-004) or the value is one it does not admit.
    /// </returns>
    /// <remarks>
    /// The caller gates, audits and alerts on the change; the store only puts it in
    /// force, and refuses a protected key whatever the caller asks.
    /// </remarks>
    ValueTask<Result<TValue>> WriteAsync<TValue>(Setting<TValue> setting, TValue value, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the value in force for one member of a key that exists once per
    /// organization or once per host-declared category.
    /// </summary>
    /// <typeparam name="TValue">The type of the member's value.</typeparam>
    /// <param name="family">The family, from <see cref="Settings"/>.</param>
    /// <param name="parameter">The organization identifier or the declared category.</param>
    /// <param name="value">The value to put in force.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// Success, or the failure where the family is one the application cannot change
    /// (OPS-CFG-004) or the value does not read back as one the family admits.
    /// </returns>
    /// <remarks>
    /// As for a key that exists once, the caller gates, audits and alerts; the store
    /// only puts the value in force. What stood before is the value the member's route
    /// read and classified against under the row's lock, which for a family whose
    /// value the host declares is not the store's to know.
    /// </remarks>
    ValueTask<Result> WriteAsync<TValue>(
        SettingFamily<TValue> family,
        string parameter,
        TValue value,
        CancellationToken cancellationToken);
}
