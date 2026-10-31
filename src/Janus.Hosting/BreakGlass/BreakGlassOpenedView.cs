using System.Text.Json.Serialization;

namespace Janus.Hosting.BreakGlass;

/// <summary>
/// What the presentation of the credential answers once it opened a session.
/// </summary>
/// <param name="ContinueTo">
/// The absolute address of the continuation, where the browser had an authorization
/// waiting, and left out everywhere else.
/// </param>
/// <remarks>Implements OPS-BOOT-002 and AUTH-SESS-012 (D-224).</remarks>
internal sealed record BreakGlassOpenedView(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ContinueTo);
