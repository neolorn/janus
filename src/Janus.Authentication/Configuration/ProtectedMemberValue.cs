using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Configuration;

/// <summary>
/// A value named on the server for one organization's member of a protected family.
/// </summary>
/// <typeparam name="TValue">The type of a member's value.</typeparam>
/// <param name="family">The family.</param>
/// <param name="organization">The organization whose member it is.</param>
/// <param name="value">What the member becomes.</param>
/// <remarks>
/// Implements OPS-CFG-004 and chapter 10 section 4.8, whose one family,
/// <c>stepup.enforcement.&lt;organization&gt;</c>, has one key per organization identifier.
/// </remarks>
internal sealed class ProtectedMemberValue<TValue>(
    SettingFamily<TValue> family,
    OrganizationId organization,
    TValue value) : ProtectedValue
{
    /// <inheritdoc/>
    public override ConfigurationKey Key => family.For(organization.ToString());

    /// <inheritdoc/>
    public override string Written => family.Write(value);

    /// <inheritdoc/>
    public override OrganizationId? Organization => organization;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The configuration is absent.</exception>
    public override async ValueTask<bool> LoosensAsync(IConfigurationStore configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return (await configuration
                .ReadAsync(family, organization.ToString(), cancellationToken)
                .ConfigureAwait(false))
            .Match(
                before => family.Loosens(before, value),
                failure => failure.Code != ErrorCodes.StartupDeclarationMissing);
    }
}
