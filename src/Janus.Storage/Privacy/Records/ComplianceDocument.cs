using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Storage.Privacy.Records;

/// <summary>
/// How the assessment references are written into their column.
/// </summary>
/// <remarks>
/// Implements PRIV-ROPA-001 and CONV-CODE-004. Generated at build time rather than
/// reflected over at run time.
/// </remarks>
[JsonSerializable(typeof(IReadOnlyList<string>))]
internal sealed partial class ComplianceDocument : JsonSerializerContext;
