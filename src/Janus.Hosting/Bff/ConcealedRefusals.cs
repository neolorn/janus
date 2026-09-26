using System;
using System.Collections.Generic;
using Janus.Authorization.Gate;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Janus.Hosting.Bff;

/// <summary>
/// What stage 11 needs of one request to answer a refusal the gate concealed: which
/// refusal it was, and what the response carried when the request reached the
/// endpoints.
/// </summary>
/// <remarks>
/// Implements AUTHZ-CONCEAL-001, AUTHZ-CONCEAL-004, BFF-ERR-003 and OPS-ENV-002. One is
/// held per request. The first refusal concealed is the one answered, so its identifier
/// is the one the caller holds and support resolves.
/// </remarks>
internal sealed class ConcealedRefusals : IConcealedRefusals
{
    private readonly HeaderDictionary _reached = new();

    /// <summary>
    /// The audit record of the refusal to answer, or nothing while none was concealed.
    /// </summary>
    public AuditRecordId? Correlation { get; private set; }

    /// <inheritdoc/>
    public void Concealed(AuditRecordId correlation) => Correlation ??= correlation;

    /// <summary>
    /// Takes what the response carries as the request reaches the endpoints.
    /// </summary>
    /// <param name="written">The response's headers.</param>
    /// <exception cref="ArgumentNullException">The headers are absent.</exception>
    public void Reached(IHeaderDictionary written)
    {
        ArgumentNullException.ThrowIfNull(written);

        foreach (KeyValuePair<string, StringValues> header in written)
        {
            _reached[header.Key] = header.Value;
        }
    }

    /// <summary>
    /// Writes back what the response carried as the request reached the endpoints.
    /// </summary>
    /// <param name="cleared">The response's headers, cleared of what the endpoint wrote.</param>
    /// <exception cref="ArgumentNullException">The headers are absent.</exception>
    public void Restore(IHeaderDictionary cleared)
    {
        ArgumentNullException.ThrowIfNull(cleared);

        foreach (KeyValuePair<string, StringValues> header in _reached)
        {
            cleared[header.Key] = header.Value;
        }
    }
}
