using System;

namespace Janus.Conformance;

/// <summary>
/// A client registered in the provider's registry, which the provider's refusals are
/// asked of.
/// </summary>
/// <param name="ClientId">The client's identifier.</param>
/// <param name="Secret">The secret it authenticates with, as the UTF-8 of the text it presents.</param>
/// <param name="Destination">The destination it is registered with.</param>
/// <remarks>
/// Implements AUTH-OIDC-006 AC1. The client authenticates on every probe but the one
/// that asks whether a client that does not is refused, so what is refused is the form
/// asked for and not the client.
/// </remarks>
public sealed record ConformanceClient(string ClientId, ReadOnlyMemory<byte> Secret, Uri Destination);
