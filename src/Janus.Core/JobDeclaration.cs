using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One of the host's background jobs, as the host declares it: the name its principal
/// carries and the host permissions it exercises.
/// </summary>
/// <param name="Name">
/// The job's name, under the rule of INT-SMS-003, which its organization-scoped
/// principal names. It is kept as written, so a name outside the rule, given twice, or
/// taken by a principal of chapter 10 section 5.29 or by <c>migrate</c> is refused at
/// startup naming the declaration.
/// </param>
/// <param name="Permissions">
/// The host permissions it exercises: none of the library's own, none bound to a step-up
/// gate and none whose action is <c>export</c>.
/// </param>
/// <remarks>Implements IDN-PRIN-001 and LIB-HOST-001 (D-202, D-206).</remarks>
public sealed record JobDeclaration(string Name, IReadOnlyList<Permission> Permissions);
