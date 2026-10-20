using Janus.Core;

namespace Janus.Storage;

/// <summary>
/// What a value of one of a host's own declared fields is bound to. The library reads
/// nothing of the host's tables, so the declared names stand where a table and a column
/// stand for a value of its own, and bind the value as tightly.
/// </summary>
/// <param name="Subject">The subject whose key the value is encrypted under.</param>
/// <param name="Type">The declared name of the type the field belongs to.</param>
/// <param name="Field">The field's declared name.</param>
/// <remarks>Implements PRIV-RIGHT-005a (D-203).</remarks>
internal readonly record struct HostFieldLocation(SubjectId Subject, ResourceType Type, string Field);
