using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
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
/// heard costs promptness and never an event. The stream speaks at once and at least
/// every <see cref="Heartbeat"/>, a comment line where it has nothing else to send, so a
/// client knows it is connected and an idle proxy keeps it open (D-224).
/// </remarks>
internal static class RegistrationStream
{
    /// <summary>
    /// The longest the stream stays silent (chapter 09, <c>GET /register/events</c>).
    /// </summary>
    public static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Answers a browser that carries no registration session, which is a browser
    /// with nothing here to stream: the absent resource, as the library answers one
    /// (BFF-CSRF-005b AC1, chapter 09 section 2).
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The work of answering it.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static Task NothingAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Refusal.WriteAsync(context, ErrorCodes.ResourceNotFound, context.RequestAborted);
    }

    /// <summary>
    /// Streams the session until it ends or the browser goes away.
    /// </summary>
    /// <param name="registration">What reads the state.</param>
    /// <param name="session">The registration session.</param>
    /// <param name="signals">What tells the stream the session has changed.</param>
    /// <param name="configuration">Where the poll interval is read from.</param>
    /// <param name="time">The clock the silence is measured on.</param>
    /// <param name="context">The request.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of streaming it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task RunAsync(
        IRegistration registration,
        RegistrationSessionId session,
        IRegistrationSignals signals,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(context);

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-store";

        TimeSpan interval = (await configuration
                .ReadAsync(Settings.RegistrationEventsPollInterval, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        await SpokenAsync(context, cancellationToken).ConfigureAwait(false);

        DateTimeOffset spoken = time.GetUtcNow();
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

                spoken = time.GetUtcNow();
            }
            else if (time.GetUtcNow() - spoken >= Heartbeat)
            {
                await SpokenAsync(context, cancellationToken).ConfigureAwait(false);

                spoken = time.GetUtcNow();
            }

            last = written;

            TimeSpan left = Heartbeat - (time.GetUtcNow() - spoken);

            await signals
                .WaitAsync(session, Waited(interval, left), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    // The poll interval, or less where the silence would otherwise pass the heartbeat.
    private static TimeSpan Waited(TimeSpan interval, TimeSpan left) =>
        left <= TimeSpan.Zero ? TimeSpan.Zero : left < interval ? left : interval;

    // The three events D-153 names. Which one it is is the difference between the
    // state before and the state now; the data is the state either way.
    private static string Named(string before, string now) =>
        Step(before) == Step(now) ? "identifier-verified" : "step-completed";

    private static string Step(string written)
    {
        using var read = JsonDocument.Parse(written);

        return read.RootElement.GetProperty("step").GetString() ?? string.Empty;
    }

    // A comment line: it carries nothing and tells the client the stream is open.
    private static async ValueTask SpokenAsync(HttpContext context, CancellationToken cancellationToken)
    {
        await context.Response.WriteAsync(":\n\n", cancellationToken).ConfigureAwait(false);

        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
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
