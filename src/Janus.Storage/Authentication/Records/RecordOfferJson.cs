using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// How the values an offer to attach was proved by are written into its row.
/// </summary>
/// <remarks>
/// Implements REC-ATT-001. The shape is generated at build time rather than reflected
/// over at run time, so the column's format is fixed by something a reader can see.
/// </remarks>
[JsonSerializable(typeof(IReadOnlyList<ProvenValueDocument>))]
internal sealed partial class RecordOfferJson : JsonSerializerContext;
