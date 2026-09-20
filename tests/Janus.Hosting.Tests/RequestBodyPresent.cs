using Microsoft.AspNetCore.Http.Features;

namespace Janus.Hosting.Tests;

/// <summary>
/// Tells the framework that a request carries a body, which the web server's own
/// feature tells it from the wire and a request built in memory has to declare.
/// </summary>
internal sealed class RequestBodyPresent : IHttpRequestBodyDetectionFeature
{
    /// <inheritdoc/>
    public bool CanHaveBody => true;
}
