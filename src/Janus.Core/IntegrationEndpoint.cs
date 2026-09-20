using System;

namespace Janus.Core;

/// <summary>
/// An address the deployment calls out to, declared so that it can be checked before
/// anything is sent to it.
/// </summary>
/// <param name="Integration">Which integration it belongs to.</param>
/// <param name="Key">The setting the address was read from.</param>
/// <param name="Address">Where the integration is called.</param>
/// <remarks>
/// Implements INT-GEN-001 and LIB-EXT-001. A published environment file specifying a
/// plain address is the ordinary way customer data ends up unencrypted on the wire,
/// so the check is at startup and not at the call.
/// </remarks>
public sealed record IntegrationEndpoint(string Integration, string Key, Uri Address);
