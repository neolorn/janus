using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Storage.Authentication.Configuration;

/// <summary>
/// The shape of the document a configuration change is looked up by, which is the one
/// field the trail is asked to contain.
/// </summary>
/// <remarks>Implements OPS-CFG-005 and CONV-CODE-005.</remarks>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class AuditQuery : JsonSerializerContext;
