using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Registration;

/// <summary>
/// The server-sent events of a registration session.
/// </summary>
/// <remarks>
/// Implements BFF-CSRF-005b, REG-SESS-003 and FE-VER-001. The stream is
/// authenticated by the cookie alone: no token is in the URL, so nothing of it
/// reaches a proxy log, a history or a referrer. What it carries is the state
/// document of <c>GET /register</c>, so a frontend that falls back to polling reads
/// the same shape. The state is read back rather than signalled in process, because
/// the browser that presses a verification link is not promised to reach the
/// instance the stream is open on.
/// </remarks>
internal static class RegistrationStream
{
    // Short enough that a press feels immediate to the person waiting, long enough
    // that a waiting screen is not a load generator.
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Answers a browser that carries no registration session, which is a browser
    /// with nothing here to stream (BFF-CSRF-005b AC1).
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The work of answering it.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static Task NothingAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.StatusCode = StatusCodes.Status404NotFound;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Streams the session until it ends or the browser goes away.
    /// </summary>
    /// <param name="registration">What reads the state.</param>
    /// <param name="session">The registration session.</param>
    /// <param name="time">What the interval is waited on.</param>
    /// <param name="context">The request.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of streaming it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task RunAsync(
        IRegistration registration,
        RegistrationSessionId session,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(context);

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-store";

        string? last = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            RegistrationStateView? state = (await registration
                    .StateAsync(session, cancellationToken)
                    .ConfigureAwait(false))
                .Match(RegistrationStateView.Of, _ => (RegistrationStateView?)null);

            if (state is null)
            {
                await SentAsync(context, "session-ended", "{}", cancellationToken)
                    .ConfigureAwait(false);

                return;
            }

            string written = JsonSerializer.Serialize(
                state,
                RegistrationJson.Default.RegistrationStateView);

            if (last is not null && !string.Equals(last, written, StringComparison.Ordinal))
            {
                await SentAsync(context, Named(last, written), written, cancellationToken)
                    .ConfigureAwait(false);
            }

            last = written;

            await Task.Delay(Interval, time, cancellationToken).ConfigureAwait(false);
        }
    }

    // The three events D-153 names. Which one it is is the difference between the
    // state before and the state now; the data is the state either way.
    private static string Named(string before, string now) =>
        Step(before) == Step(now) ? "identifier-verified" : "step-completed";

    private static string Step(string written)
    {
        using var read = JsonDocument.Parse(written);

        return read.RootElement.GetProperty("step").GetString() ?? string.Empty;
    }

    private static async ValueTask SentAsync(
        HttpContext context,
        string name,
        string data,
        CancellationToken cancellationToken)
    {
        await context.Response
            .WriteAsync("event: " + name + "\ndata: " + data + "\n\n", cancellationToken)
            .ConfigureAwait(false);

        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
