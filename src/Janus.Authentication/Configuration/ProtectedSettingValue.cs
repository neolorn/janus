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
    public override OrganizationId? Organization => null;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The configuration is absent.</exception>
    public override async ValueTask<bool> LoosensAsync(IConfigurationStore configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false)).Match(
            before => setting.Loosens(before, value),
            failure => failure.Code != ErrorCodes.StartupDeclarationMissing);
    }
}
