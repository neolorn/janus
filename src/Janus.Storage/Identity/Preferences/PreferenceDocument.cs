using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Storage.Identity.Preferences;

/// <summary>
/// How the declared preference values are written into their one encrypted column.
/// </summary>
/// <remarks>
/// Implements REG-PREF-001. The shape is generated at build time rather than reflected
/// over at run time, so the column's format is fixed by something a reader can see.
/// </remarks>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class PreferenceDocument : JsonSerializerContext;
