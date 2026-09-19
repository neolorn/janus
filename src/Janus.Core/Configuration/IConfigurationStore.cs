using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core.Configuration;

/// <summary>
/// Where the runtime-changeable keys of chapter 10 section 4 are read from. Every
/// such key is read through this, never from bound options, so a change through the
/// management application takes effect without a restart.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-DESIGN-005, OPS-CFG-001, OPS-CFG-008. A key the
/// deployment never wrote reads as its default; a stored value the key no longer
/// admits, which a tightened floor can leave behind, is a failure the caller handles
/// rather than a value it receives.
/// </remarks>
public interface IConfigurationStore
{
    /// <summary>
    /// Reads a key that exists once for the deployment.
    /// </summary>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <param name="setting">The setting, from <see cref="Settings"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The value in force, or the failure naming the constraint it misses.</returns>
    ValueTask<Result<TValue>> ReadAsync<TValue>(Setting<TValue> setting, CancellationToken cancellationToken);

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
    /// Implements OPS-CFG-008 and OPS-CFG-004. The caller gates, audits and alerts on
    /// the change; the store only puts it in force, and refuses a protected key
    /// whatever the caller asks.
    /// </remarks>
    ValueTask<Result<TValue>> WriteAsync<TValue>(Setting<TValue> setting, TValue value, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one member of a key that exists once per organization or once per
    /// host-declared category.
    /// </summary>
    /// <typeparam name="TValue">The type of the member's value.</typeparam>
    /// <param name="family">The family, from <see cref="Settings"/>.</param>
    /// <param name="parameter">The organization identifier or the declared category.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The value in force for that member, or the failure where the member has no
    /// value and the family has no default.
    /// </returns>
    ValueTask<Result<TValue>> ReadAsync<TValue>(SettingFamily<TValue> family, string parameter, CancellationToken cancellationToken);
}
