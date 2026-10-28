using System.Text.Json.Serialization;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// How a staged collection is written into its one encrypted column.
/// </summary>
/// <remarks>
/// Implements REC-COL-001. The shape is generated at build time rather than reflected
/// over at run time, so the column's format is fixed by something a reader can see.
/// </remarks>
[JsonSerializable(typeof(CollectionSessionDocument))]
internal sealed partial class CollectionSessionJson : JsonSerializerContext;
