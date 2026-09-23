using System.Text.Json;

namespace Janus.Hosting.Organizations;

/// <summary>
/// One field of an organization's policy, or one gate of it, as the response carries
/// it: the value in force, and whether that value is the organization's own.
/// </summary>
/// <param name="Value">The value in force, in the form chapter 10 section 4.1a writes it.</param>
/// <param name="Overridden">
/// Whether the value in force is the organization's own; otherwise it is inherited
/// from the system policy.
/// </param>
/// <remarks>Implements chapter 09 section 8a and D-143.</remarks>
internal sealed record PolicyFieldView(JsonElement Value, bool Overridden);
