using Janus.Core;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The code that confirms the enrolment of a text code.
/// </summary>
/// <param name="Code">What was typed.</param>
/// <remarks>Implements AUTH-FACT-002.</remarks>
internal sealed record ConfirmTextCodeRequest([property: NeverLogged] string? Code);
