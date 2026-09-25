using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Janus.Hosting.Callbacks;

/// <summary>
/// One inbound callback as the machine profile read it: the raw bytes of its body,
/// untouched by any parser, beside its headers and query string.
/// </summary>
/// <param name="Method">The method it arrived with.</param>
/// <param name="Query">Its query string.</param>
/// <param name="Headers">Its headers, by name, compared without regard to case.</param>
/// <param name="Body">Its body, exactly as the provider sent it.</param>
/// <remarks>
/// Implements BFF-MACH-002 and INT-GEN-003. A signature is verified over these bytes and
/// never over a body that was parsed and written again, which would not be the bytes
/// the provider signed.
/// </remarks>
public sealed record CallbackDelivery(
    string Method,
    IQueryCollection Query,
    IReadOnlyDictionary<string, StringValues> Headers,
    ReadOnlyMemory<byte> Body);
