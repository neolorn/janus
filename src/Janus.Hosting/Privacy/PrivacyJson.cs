using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Hosting.Privacy;

/// <summary>
/// How the privacy documents are written, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements PRIV-CONS-005, PRIV-CONS-006 and PRIV-CONS-011.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(DocumentVersionView))]
[JsonSerializable(typeof(IReadOnlyList<ConsentView>))]
[JsonSerializable(typeof(IReadOnlyList<ObjectionView>))]
[JsonSerializable(typeof(PrivacyReceiptView))]
[JsonSerializable(typeof(IReadOnlyList<PrivacyRequestView>))]
[JsonSerializable(typeof(ExportView))]
[JsonSerializable(typeof(PortableExportView))]
[JsonSerializable(typeof(ProcessingRegisterView))]
internal sealed partial class PrivacyJson : JsonSerializerContext;
