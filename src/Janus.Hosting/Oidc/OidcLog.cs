using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What the provider records about a request it refused.
/// </summary>
/// <remarks>
/// Implements API-REDIR-001 AC4 and CONV-LOG-003. A client identifier and a destination
/// are the deployment's own arrangement and not personal data, so both are written;
/// nothing about the person is.
/// </remarks>
internal static partial class OidcLog
{
    /// <summary>
    /// A pushed request named a destination that is not the client's registered one,
    /// and was refused.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="clientId">Which client asked.</param>
    /// <param name="destination">What it asked for.</param>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Authorization request from {ClientId} named {Destination}, which is not its registered destination, and was refused ({CorrelationId}).")]
    public static partial void DestinationRefused(ILogger log, string correlationId, string clientId, string destination);
}
