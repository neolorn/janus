using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What the provider records about a request it corrected.
/// </summary>
/// <remarks>
/// Implements API-REDIR-001 and CONV-LOG-003. A client identifier and a destination
/// are the deployment's own arrangement and not personal data, so both are written;
/// nothing about the person is.
/// </remarks>
internal static partial class OidcLog
{
    /// <summary>
    /// A destination that is not the client's registered one was replaced by it.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="clientId">Which client asked.</param>
    /// <param name="destination">What it asked for.</param>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Authorization request from {ClientId} named {Destination}, which is not its registered destination; the registered one was used.")]
    public static partial void DestinationReplaced(ILogger log, string clientId, string destination);
}
