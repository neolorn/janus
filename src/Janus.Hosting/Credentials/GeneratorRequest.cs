namespace Janus.Hosting.Credentials;

/// <summary>
/// What the person calls the generator being enrolled.
/// </summary>
/// <param name="Label">What the person calls it.</param>
/// <remarks>Implements AUTH-FACT-001 and AUTH-FACT-007.</remarks>
internal sealed record GeneratorRequest(string? Label);
