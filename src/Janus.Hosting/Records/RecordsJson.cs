using System.Text.Json.Serialization;

namespace Janus.Hosting.Records;

/// <summary>
/// The shapes the person-record endpoints of chapter 09 sections 7a and 8a read and
/// write.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, REC-COL-001 and REC-COL-003. Generated at build time, so the shapes
/// are fixed by something a reader can see.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(BeginCollectionRequest))]
[JsonSerializable(typeof(CollectionAgeRequest))]
[JsonSerializable(typeof(CollectionContactRequest))]
[JsonSerializable(typeof(CollectionVerifyRequest))]
[JsonSerializable(typeof(CollectionStateView))]
[JsonSerializable(typeof(RecordEntryBody))]
[JsonSerializable(typeof(EnteredRecordView))]
internal sealed partial class RecordsJson : JsonSerializerContext;
