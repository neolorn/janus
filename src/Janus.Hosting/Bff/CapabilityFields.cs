using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Janus.Core;
using Janus.Hosting.Authorization;

namespace Janus.Hosting.Bff;

/// <summary>
/// What a host's record carries of the capabilities its caller holds on it, as its
/// members <c>can</c> and <c>requires</c>.
/// </summary>
/// <remarks>
/// Implements BFF-CAP-002 and API-CAP-001 (D-206). Each entry is a permission's action,
/// so no permission name reaches the browser (BFF-SESS-001), and each residual is its
/// chapter 10 section 5.20 name. <see cref="Can"/>, the keys of <see cref="Requires"/>
/// and each residual list are in ordinal order, and a capability with nothing
/// outstanding is absent from <see cref="Requires"/>.
/// </remarks>
public sealed record CapabilityFields
{
    private CapabilityFields(IReadOnlyList<string> can, IReadOnlyDictionary<string, IReadOnlyList<string>> requires)
    {
        Can = can;
        Requires = requires;
    }

    /// <summary>
    /// The actions the caller's grants confer on the record.
    /// </summary>
    public IReadOnlyList<string> Can { get; }

    /// <summary>
    /// What each of those actions still requires before it will succeed.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Requires { get; }

    /// <summary>
    /// The fields of one record's capability.
    /// </summary>
    /// <param name="capability">What the gate answered for the record.</param>
    /// <returns>The fields.</returns>
    /// <exception cref="ArgumentNullException">The capability is absent.</exception>
    /// <exception cref="ArgumentException">
    /// The capability holds two permissions of one action, which no type the model
    /// starts with declares.
    /// </exception>
    public static CapabilityFields Of(Capability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        var can = new SortedSet<string>(StringComparer.Ordinal);
        var requires = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (Permission permission in capability.Can)
        {
            if (!can.Add(permission.Action))
            {
                throw new ArgumentException(
                    "The capability holds two permissions of the action " + permission.Action + ".",
                    nameof(capability));
            }

            if (capability.Requires.TryGetValue(permission, out IReadOnlySet<CapabilityResidual>? residuals)
                && residuals.Count > 0)
            {
                requires[permission.Action] = [.. residuals.Select(Named).Order(StringComparer.Ordinal)];
            }
        }

        return new CapabilityFields([.. can], new ReadOnlyDictionary<string, IReadOnlyList<string>>(requires));
    }

    // The residual as chapter 10 section 5.20 spells it, which is how the library
    // writes it everywhere.
    private static string Named(CapabilityResidual residual) =>
        JsonSerializer.SerializeToElement(residual, AuthorizationJson.Default.CapabilityResidual).GetString()!;
}
