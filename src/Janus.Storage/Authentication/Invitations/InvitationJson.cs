using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// How an invitation's identifiers and documents are written into their columns.
/// </summary>
/// <remarks>
/// Implements REG-INV-001 and CONV-CODE-004. Generated at build time rather than
/// reflected over at run time, so the columns' format is fixed by something a reader
/// can see.
/// </remarks>
[JsonSerializable(typeof(InvitedIdentifiersDocument))]
[JsonSerializable(typeof(IReadOnlyList<InvitedDocument>))]
internal sealed partial class InvitationJson : JsonSerializerContext;
