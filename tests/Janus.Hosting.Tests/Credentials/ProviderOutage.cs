namespace Janus.Hosting.Tests.Credentials;

/// <summary>
/// How a part of a social provider fails a request made of it.
/// </summary>
internal enum ProviderOutage
{
    /// <summary>The connection is refused, so no response comes back.</summary>
    Refused = 0,

    /// <summary>Nothing answers before the client gives up waiting.</summary>
    Silent = 1,

    /// <summary>It answers, with what cannot be read as the document asked for.</summary>
    Unreadable = 2,

    /// <summary>It answers a document that names its issuer and keys and no endpoint to sign in at.</summary>
    Endpointless = 3,
}
