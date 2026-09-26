using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Janus.Storage.Authentication.Alerting;

/// <summary>
/// How the details of a raised condition are written and read, generated rather than
/// reflected over (CONV-CODE-004).
/// </summary>
/// <remarks>Implements OPS-ALERT-001.</remarks>
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal sealed partial class RaisedAlertJson : JsonSerializerContext;
