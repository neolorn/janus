using System.Collections.Generic;

namespace Janus.Authentication.Passwords;

/// <summary>
/// What is worth telling the person about a password, none of which refuses it. The
/// frontend writes the words; this carries what they are about.
/// </summary>
/// <param name="Weak">
/// Whether a strength heuristic scored it poorly. A password that meets the floor and
/// is absent from the rejection sources is accepted whatever this says.
/// </param>
/// <param name="OwnWords">
/// The person's own words, and the service name, that appear in it.
/// </param>
/// <remarks>
/// Implements AUTH-PASS-005 and CONV-CONTENT-001. Rejection happens for a floor or a
/// rejection source and never for a heuristic: heuristic rejection generates
/// workarounds, and refusing a password for containing the person's own name is a
/// composition rule by another name.
/// </remarks>
internal sealed record PasswordFeedback(bool Weak, IReadOnlyList<string> OwnWords);
