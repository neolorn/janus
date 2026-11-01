using System.Text.Json.Serialization;

namespace Janus.Hosting.Registration;

/// <summary>
/// What a registration's completion answers.
/// </summary>
/// <param name="ContinueTo">
/// The absolute address of the continuation where the browser's pending authorization was
/// carried to the session the completion established; absent otherwise.
/// </param>
/// <remarks>Implements AUTH-SESS-012 and REG-SESS-008 (D-242).</remarks>
internal sealed record RegistrationCompletedView(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ContinueTo);
