using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting;

/// <summary>
/// Reads what the host's logging makes of the category the server writes its
/// per-request entries under, before the web server starts.
/// </summary>
/// <param name="logging">The logging the host built, every rule and provider in it.</param>
/// <param name="providers">The providers the host registered.</param>
/// <remarks>
/// Implements BFF-LOG-002 and CONV-LOG-002. The server's entries for a request carry
/// its query string, which on a navigation's return holds an authorization code, so
/// the registration sets their category to Warning. The server opens the scope that
/// carries <c>RequestId</c> while that category is enabled at any level in any
/// provider, so a host that switches it off everywhere loses the identifier from every
/// entry. Either is found here rather than in a log read after the fact.
/// </remarks>
internal sealed class RequestLoggingValidationService(
    ILoggerFactory logging,
    IEnumerable<ILoggerProvider> providers) : IHostedService
{
    /// <summary>
    /// The category the server writes its per-request entries under, and reads to
    /// decide whether it opens the request's scope.
    /// </summary>
    internal const string Category = "Microsoft.AspNetCore.Hosting.Diagnostics";

    /// <inheritdoc/>
    /// <exception cref="StartupException">
    /// The host's logging enables the category at Information through a provider, or
    /// disables it in every provider it registered.
    /// </exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // The logger the factory answers is enabled at a level where any provider's
        // is, under that provider's own rule, which is how the server reads it too.
        ILogger entries = logging.CreateLogger(Category);

        if (entries.IsEnabled(LogLevel.Information))
        {
            throw new StartupException(
                "The host's logging enables the category of the server's per-request entries at Information, which writes each request's query string.",
                Error.From(ErrorCodes.StartupRequestLogging));
        }

        // A host that registered no provider writes no entry at all, and disables
        // nothing.
        if (!entries.IsEnabled(LogLevel.Critical) && providers.Any())
        {
            throw new StartupException(
                "The host's logging disables the category of the server's per-request entries in every provider, which drops the scope that carries the request identifier.",
                Error.From(ErrorCodes.StartupRequestLogging));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
