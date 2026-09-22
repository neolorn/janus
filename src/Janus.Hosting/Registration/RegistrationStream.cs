using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Core.Configuration;
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
/// the same shape. What wakes the stream is the database channel, raised in the
/// transaction that verified or completed the step, because the browser that presses
/// a verification link is not promised to reach the instance the stream is open on.
/// The state is read back on the poll interval as well, so a channel that is not
/// heard costs promptness and never an event.
/// </remarks>
internal static class RegistrationStream
{

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
    /// <param name="signals">What tells the stream the session has changed.</param>
    /// <param name="configuration">Where the poll interval is read from.</param>
    /// <param name="context">The request.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of streaming it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task RunAsync(
        IRegistration registration,
        RegistrationSessionId session,
        IRegistrationSignals signals,
        IConfigurationStore configuration,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(context);

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-store";

        TimeSpan interval = (await configuration
                .ReadAsync(Settings.RegistrationEventsPollInterval, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, _ => Settings.RegistrationEventsPollInterval.Default);

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

            await signals
                .WaitAsync(session, interval, cancellationToken)
                .ConfigureAwait(false);
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
