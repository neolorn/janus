using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Janus.Storage.Identity.Audit;

/// <summary>
/// How an audit record's structured fields are written, in the plain column and in the
/// encrypted one.
/// </summary>
/// <remarks>
/// Implements PRIV-RET-002 and PRIV-RET-004. The shape is generated at build time
/// rather than reflected over at run time, so the columns' format is fixed by something
/// a reader can see.
/// </remarks>
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal sealed partial class AuditDocument : JsonSerializerContext;
