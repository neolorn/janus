using System;
using System.Collections.Generic;
using System.Linq;

namespace Janus.Core;

/// <summary>
/// Every outbound address the deployment declared, and which of them are not TLS.
/// </summary>
/// <remarks>
/// Implements INT-GEN-001. The library calls nothing of its own: a deployment that
/// registers a transport declares where it reaches, and startup refuses a plaintext
/// one rather than discovering it in a packet capture.
/// </remarks>
public sealed class IntegrationEndpoints
{
    private readonly IReadOnlyList<IntegrationEndpoint> _declared;

    private IntegrationEndpoints(IReadOnlyList<IntegrationEndpoint> declared) =>
        _declared = declared;

    /// <summary>
    /// The deployment that declares none.
    /// </summary>
    public static IntegrationEndpoints None { get; } = new([]);

    /// <summary>
    /// Every endpoint declared.
    /// </summary>
    public IReadOnlyList<IntegrationEndpoint> All => _declared;

    /// <summary>
    /// The endpoints that would carry their traffic in the clear.
    /// </summary>
    public IReadOnlyList<IntegrationEndpoint> Insecure =>
        [.. _declared.Where(endpoint =>
            !string.Equals(endpoint.Address.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))];

    /// <summary>
    /// Takes the declared endpoints as one collection.
    /// </summary>
    /// <param name="declared">What the host declared.</param>
    /// <returns>The collection the library checks.</returns>
    /// <exception cref="ArgumentNullException">The sequence or a member is absent.</exception>
    public static IntegrationEndpoints Of(IEnumerable<IntegrationEndpoint> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        var endpoints = new List<IntegrationEndpoint>();

        foreach (IntegrationEndpoint endpoint in declared)
        {
            ArgumentNullException.ThrowIfNull(endpoint);

            endpoints.Add(endpoint);
        }

        return new IntegrationEndpoints(endpoints);
    }
}
