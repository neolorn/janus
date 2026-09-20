using System.Text.Json.Serialization;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// How a staged registration is written into its one encrypted column.
/// </summary>
/// <remarks>
/// Implements REG-SESS-001. The shape is generated at build time rather than
/// reflected over at run time, so the column's format is fixed by something a reader
/// can see.
/// </remarks>
[JsonSerializable(typeof(StagedSessionDocument))]
internal sealed partial class StagedSession : JsonSerializerContext;
