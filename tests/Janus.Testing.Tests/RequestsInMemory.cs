using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Testing.Tests;

/// <summary>
/// Every request the browsers of a test made to the application, each carried on to the
/// application as it was made, so a test can say what the people of a deployment sent.
/// </summary>
internal sealed class RequestsInMemory
{
    private readonly ConcurrentQueue<RequestMade> _made = new();

    /// <summary>
    /// Every request made, in order.
    /// </summary>
    public IReadOnlyCollection<RequestMade> Made => _made;

    /// <summary>
    /// What carries a browser's requests to the application over a channel, keeping each.
    /// </summary>
    /// <param name="channel">What reaches the application.</param>
    /// <returns>The handler, which disposes the channel with it.</returns>
    public HttpMessageHandler Over(HttpMessageHandler channel) => new Keeping(channel, _made);

    private sealed class Keeping(HttpMessageHandler channel, ConcurrentQueue<RequestMade> made)
        : DelegatingHandler(channel)
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            made.Enqueue(new RequestMade(request.Method, request.RequestUri?.AbsolutePath ?? string.Empty, body));

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
