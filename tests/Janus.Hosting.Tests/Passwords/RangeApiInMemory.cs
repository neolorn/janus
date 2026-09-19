using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Hosting.Tests.Passwords;

/// <summary>
/// The range API, answering from what a test put in it and recording what was asked.
/// </summary>
internal sealed class RangeApiInMemory : HttpMessageHandler
{
    private readonly Dictionary<string, string> _ranges = new(StringComparer.Ordinal);
    private readonly List<Uri> _asked = [];

    /// <summary>
    /// Whether the service is reachable at all.
    /// </summary>
    public bool Reachable { get; set; } = true;

    /// <summary>
    /// Every address the corpus asked, in order.
    /// </summary>
    public IReadOnlyList<Uri> Asked => _asked;

    /// <summary>
    /// Names what the service answers under one prefix.
    /// </summary>
    /// <param name="prefix">The five characters the range is held under.</param>
    /// <param name="body">The body, as the provider publishes it.</param>
    public void Holds(string prefix, string body) => _ranges[prefix] = body;

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Reachable)
        {
            throw new HttpRequestException("The range API is unreachable.");
        }

        Uri address = request.RequestUri
            ?? throw new InvalidOperationException("The request names no address.");

        _asked.Add(address);

        string prefix = address.Segments[^1];

        return Task.FromResult(_ranges.TryGetValue(prefix, out string? body)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
