using System.Collections.Generic;
using System.Text.Json;

namespace Janus.Hosting.Bff;

/// <summary>
/// What an error answers with: a code the frontend renders from, the identifier the
/// logs and the audit resolve by, and structured context.
/// </summary>
/// <param name="Code">The machine-readable code.</param>
/// <param name="CorrelationId">What resolves this request in the logs.</param>
/// <param name="Details">Structured context, never a sentence.</param>
/// <remarks>
/// Implements API-CONV-002 and LIB-API-003. The details are the failure's own, passed
/// across unchanged: the boundary adds nothing to them and removes nothing from them
/// (BFF-STEP-001).
/// </remarks>
internal sealed record ApiError(
    string Code,
    string CorrelationId,
    IReadOnlyDictionary<string, JsonElement> Details);
