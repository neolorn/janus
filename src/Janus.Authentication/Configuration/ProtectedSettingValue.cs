using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Configuration;

/// <summary>
/// A value named on the server for a protected key that exists once for the deployment.
/// </summary>
/// <typeparam name="TValue">The type of the key's value.</typeparam>
/// <param name="setting">The key.</param>
/// <param name="value">What it becomes.</param>
/// <remarks>Implements OPS-CFG-004 and chapter 10 section 4.8.</remarks>
internal sealed class ProtectedSettingValue<TValue>(Setting<TValue> setting, TValue value) : ProtectedValue
{
    /// <inheritdoc/>
    public override ConfigurationKey Key => setting.Key;

    /// <inheritdoc/>
    public override string Written => setting.Write(value);

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The configuration is absent.</exception>
    public override async ValueTask<bool> LoosensAsync(IConfigurationStore configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // A key the deployment has not named yet has no value in force to loosen; a
        // stored value that does not read is the store's fault, thrown there.
        return (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false)).Match(
            before => setting.Loosens(before, value),
            _ => false);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The configuration is absent.</exception>
    public override async ValueTask<string?> BeforeAsync(IConfigurationStore configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // A read answers only a key the deployment names that has no row; a stored
        // value that does not read is the store's fault, thrown there.
        return (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false)).Match(
            before => setting.Write(before),
            _ => (string?)null);
    }
}
