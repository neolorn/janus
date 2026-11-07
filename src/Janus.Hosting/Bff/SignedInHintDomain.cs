using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core.Configuration;

namespace Janus.Hosting.Bff;

/// <summary>
/// The parent domain the signed-in hint is kept on, as the deployment's start read it.
/// </summary>
/// <remarks>
/// Implements BFF-SESS-006 and PRIV-CONS-006a (D-279). <c>session.hint.domain</c> is
/// protected, so it changes only through <c>configure</c>, and it is read once, at the
/// start, as <c>integration.mailserver.endpoint</c> is: a change takes effect at the next
/// start, whose notice comparison raises the hint where the notice does not list it. The
/// hint and the deployment's cookie list both answer to the value held here, so neither
/// moves before the start does. Before a start has read it, no domain is held.
/// </remarks>
internal sealed class SignedInHintDomain
{
    private string? _domain;

    /// <summary>
    /// The domain the start read, or nothing where the key names none.
    /// </summary>
    public string? Domain => Volatile.Read(ref _domain);

    /// <summary>
    /// Reads the key, which a start does once.
    /// </summary>
    /// <param name="configuration">Where the key is read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of reading it.</returns>
    /// <exception cref="ArgumentNullException">The configuration is absent.</exception>
    /// <exception cref="InvalidOperationException">The key cannot be read.</exception>
    public async ValueTask ReadAsync(IConfigurationStore configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string read = (await configuration
                .ReadAsync(Settings.SessionSignedInDomain, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        Volatile.Write(ref _domain, read.Length > 0 ? read : null);
    }
}
