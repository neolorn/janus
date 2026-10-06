using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Where the conformance suite asks the deployment's provider for each form the
/// provider must refuse, as this application's own sign-on client asks it.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-001 AC4 and AC5, AUTH-OIDC-006 AC1 and LIB-API-005 (D-172). The
/// probes are made by the library's own client half, which reads the client's current
/// secret from the registry at each request and keeps nothing, so nothing here carries
/// or receives a secret. The operation is called in process only and meets no gate:
/// the suite is run by the host, and the probes read no record of a person.
/// </remarks>
public interface IProviderProbes
{
    /// <summary>
    /// Asks every refusal of the provider at the endpoints its discovery document names.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// A finding, <c>auth.oidc.nonconformant</c> naming the probe, what was sent, the
    /// refusal expected and what came back, for each form the provider admitted or its
    /// document advertised; or the refusal met reading the client or its secret.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">The context is absent.</exception>
    ValueTask<Result<IReadOnlyList<Error>>> RunAsync(AccessContext context, CancellationToken cancellationToken);
}
