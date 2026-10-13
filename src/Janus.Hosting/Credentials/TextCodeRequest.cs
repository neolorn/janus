namespace Janus.Hosting.Credentials;

/// <summary>
/// The number a text code is being enrolled on and what the person calls it.
/// </summary>
/// <param name="Identifier">The verified phone identifier of the account, by its id.</param>
/// <param name="Label">What the person calls the credential.</param>
/// <remarks>Implements AUTH-FACT-001 and AUTH-FACT-002.</remarks>
internal sealed record TextCodeRequest(string? Identifier, string? Label);
