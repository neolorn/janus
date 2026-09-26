using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// Where an operation that changes an account's own settings asks whether the
/// account's processing is restricted.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-007 AC2 and AUTHZ-GATE-006 AC2. A restricted account reads its
/// own data and exercises its rights, and changes none of its settings. The
/// restriction is read and refused by the access gate alone, which is out of this
/// area's reach (CONV-LAYOUT-001), so each operation asks it through a port of its own
/// (CONV-DESIGN-003).
/// </remarks>
internal interface ISettingsRestriction
{
    /// <summary>
    /// The refusal a change to the account's own settings meets, if any.
    /// </summary>
    /// <param name="subject">The account whose settings would change.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns><c>authz.restricted</c> for a restricted account; otherwise nothing.</returns>
    ValueTask<Error?> RefusedAsync(SubjectId subject, CancellationToken cancellationToken);
}
