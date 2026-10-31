namespace Janus.Core;

/// <summary>
/// What asking for a sign-in link answers with.
/// </summary>
/// <param name="Challenge">
/// The handle of the sign-in the ask opened, which the code a link shows where it is
/// opened in another browser is typed on; nothing where the link went on a sign-in the
/// asking browser holds.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-003 AC6 and AUTH-ABUSE-002. Whether it names a sign-in turns on
/// the asking browser's sign-ins alone, never on the account.
/// </remarks>
public sealed record RequestedLink([property: NeverLogged] string? Challenge);
