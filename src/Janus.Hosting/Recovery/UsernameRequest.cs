using Janus.Core;

namespace Janus.Hosting.Recovery;

/// <summary>
/// The self-service recovery link whose account's username the reset form reads.
/// </summary>
/// <param name="Token">The token the message carried.</param>
/// <remarks>Implements FE-PM-002 and D-224.</remarks>
internal sealed record UsernameRequest([property: NeverLogged] string? Token);
