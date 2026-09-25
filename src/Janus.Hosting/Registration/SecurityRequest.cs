using Janus.Core;

namespace Janus.Hosting.Registration;

/// <summary>
/// The security step.
/// </summary>
/// <param name="Password">The password, where one is being set.</param>
/// <param name="SecondStep">
/// The second step chosen, one of the values chapter 09 names, or nothing.
/// </param>
/// <remarks>Implements REG-SESS-006.</remarks>
internal sealed record SecurityRequest([property: NeverLogged] string? Password, string? SecondStep);
