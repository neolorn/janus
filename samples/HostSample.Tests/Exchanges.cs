using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Testing;

namespace HostSample.Tests;

/// <summary>
/// Requests a person's browser makes to the application, as its frontend makes them:
/// every route relative to where the application is mounted.
/// </summary>
internal static class Exchanges
{
    // How long a test waits for background work to show through the endpoints, and
    // how often it looks.
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// One request, and what it was answered.
    /// </summary>
    /// <param name="client">The browser.</param>
    /// <param name="method">The method.</param>
    /// <param name="route">The route, relative to where the application is mounted.</param>
    /// <param name="body">The body, sent as JSON, or nothing.</param>
    /// <param name="cancellationToken">Abandons the request.</param>
    /// <returns>What it was answered.</returns>
    public static async Task<Answered> SendAsync(
        HttpClient client,
        HttpMethod method,
        string route,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative))
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

        string written = await response.Content.ReadAsStringAsync(cancellationToken);

        return new Answered(
            response.StatusCode,
            written.Length is 0 ? null : JsonDocument.Parse(written).RootElement.Clone());
    }

    /// <summary>
    /// One request, stepped up once and made again where it is refused for a step-up,
    /// as the frontend makes it.
    /// </summary>
    /// <param name="session">The person's session.</param>
    /// <param name="method">The method.</param>
    /// <param name="route">The route, relative to where the application is mounted.</param>
    /// <param name="body">The body, sent as JSON, or nothing.</param>
    /// <param name="cancellationToken">Abandons the request.</param>
    /// <returns>What it was answered.</returns>
    public static async Task<Answered> SteppedAsync(
        TestSession session,
        HttpMethod method,
        string route,
        object? body,
        CancellationToken cancellationToken)
    {
        Answered answered = await SendAsync(session, method, route, body, cancellationToken);

        if (answered.Code is not "auth.stepup.required")
        {
            return answered;
        }

        await session.StepUpAsync(cancellationToken);

        return await SendAsync(session, method, route, body, cancellationToken);
    }

    /// <summary>
    /// Asks until the answer shows what the test waits for, which background work makes
    /// true, and answers the last answer.
    /// </summary>
    /// <param name="ask">What is asked.</param>
    /// <param name="shown">Whether the answer shows it.</param>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>The last answer.</returns>
    public static async Task<Answered> EventuallyAsync(
        Func<Task<Answered>> ask,
        Func<Answered, bool> shown,
        CancellationToken cancellationToken)
    {
        DateTimeOffset until = TimeProvider.System.GetUtcNow() + Patience;
        Answered answered = await ask();

        while (!shown(answered) && TimeProvider.System.GetUtcNow() < until)
        {
            await Task.Delay(Interval, cancellationToken);

            answered = await ask();
        }

        return answered;
    }

    /// <summary>
    /// Waits until what background work makes true is shown, or the patience runs out.
    /// </summary>
    /// <param name="shown">Whether it is shown.</param>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>The work of the wait.</returns>
    public static async Task UntilAsync(Func<bool> shown, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shown);

        DateTimeOffset until = TimeProvider.System.GetUtcNow() + Patience;

        while (!shown() && TimeProvider.System.GetUtcNow() < until)
        {
            await Task.Delay(Interval, cancellationToken);
        }
    }
}
