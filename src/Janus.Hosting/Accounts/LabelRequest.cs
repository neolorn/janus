namespace Janus.Hosting.Accounts;

/// <summary>
/// What a person calls one of their credentials.
/// </summary>
/// <param name="Label">The label.</param>
/// <remarks>Implements AUTH-FACT-001 and IDN-ATTR-008.</remarks>
internal sealed record LabelRequest(string? Label);
