using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Hosting.Records;

/// <summary>
/// The shapes the person-record endpoints of chapter 09 sections 7a and 8a read and
/// write.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, REC-COL-001, REC-COL-003, REC-ACC-001, REC-ACC-002, REC-ATT-001 and REC-ADM-001.
/// Generated at build time, so the shapes are fixed by something a reader can see.
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
[JsonSerializable(typeof(RecordFindBody))]
[JsonSerializable(typeof(RecordCorrectionBody))]
[JsonSerializable(typeof(RecordSubjectsView))]
[JsonSerializable(typeof(RecordAccessRequest))]
[JsonSerializable(typeof(RecordAccessVerifyRequest))]
[JsonSerializable(typeof(RecordAccessReferenceRequest))]
[JsonSerializable(typeof(RecordAccessStateView))]
[JsonSerializable(typeof(IReadOnlyList<AttachedRecordView>))]
[JsonSerializable(typeof(RecordOfferView))]
[JsonSerializable(typeof(RecordFindCodeRequest))]
[JsonSerializable(typeof(RecordFindVerifyRequest))]
[JsonSerializable(typeof(RecordAttachReferenceRequest))]
[JsonSerializable(typeof(RecordsAttachedView))]
[JsonSerializable(typeof(RecordRectificationRequest))]
[JsonSerializable(typeof(RecordView))]
[JsonSerializable(typeof(HeldRecordView))]
internal sealed partial class RecordsJson : JsonSerializerContext;
