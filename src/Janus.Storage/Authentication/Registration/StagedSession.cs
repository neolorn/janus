using System.Text.Json.Serialization;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// How a staged registration is written into its one encrypted column.
/// </summary>
/// <remarks>
/// Implements REG-SESS-001. The shape is generated at build time rather than
/// reflected over at run time, so the column's format is fixed by something a reader
/// can see. Only the metadata is generated: the generated writer writes an absent
/// byte array as an empty one, and a staged array reads back as it was written only
/// through the serializer's own converters (D-224).
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(StagedSessionDocument))]
internal sealed partial class StagedSession : JsonSerializerContext;
