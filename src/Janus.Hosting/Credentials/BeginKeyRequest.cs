using Janus.Core;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The kind of credential a ceremony is to create.
/// </summary>
/// <param name="Kind">A passkey or a second-factor security key.</param>
/// <remarks>Implements AUTH-FACT-002b.</remarks>
internal sealed record BeginKeyRequest(Factor? Kind);
