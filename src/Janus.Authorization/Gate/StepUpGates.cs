using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Model;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// What the step-up gate bound to an action still asks of the caller's session.
/// </summary>
/// <param name="model">The host's declaration, read for the gate an action is bound to.</param>
/// <param name="sessions">
/// Where the library's own session is judged against a gate, which a deployment whose
/// people sign in through this library has and one consuming authorization alone does not.
/// </param>
/// <param name="assurance">
/// Where how far the caller's session has authenticated is read, which a deployment
/// consuming authorization without this library's authentication supplies, and which a
/// deployment requiring no step-up leaves absent.
/// </param>
/// <remarks>
/// Implements AUTHZ-GATE-005, AUTH-STEP-001, AUTH-STEP-002, AUTH-STEP-003 and
/// LIB-HOST-004. An action bound to no gate asks nothing of the session; a bound one is
/// met by what the acting person's session has proved and by nothing else.
/// </remarks>
internal sealed class StepUpGates(
    AuthorizationModel model,
    ISessionGates? sessions,
    IAssuranceProvider? assurance)
{
    /// <summary>
    /// What the action's gate still requires, or nothing where it requires nothing.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">The permission being exercised or offered.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The refusal, or nothing where nothing is outstanding.</returns>
    public ValueTask<Error?> OutstandingAsync(
        AccessContext context,
        Permission permission,
        CancellationToken cancellationToken) =>
        OutstandingAsync(context, model.GateOf(permission), cancellationToken);

    /// <summary>
    /// What a named gate still requires, or nothing where no gate is named.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="gate">The gate's name, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The refusal, or nothing where nothing is outstanding.</returns>
    public async ValueTask<Error?> OutstandingAsync(
        AccessContext context,
        string? gate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (gate is null)
        {
            return null;
        }

        // AUTH-STEP-002: a gate is judged against the session record, so where the
        // acting person's own session carries the request it is that session's proof
        // that meets the gate or does not.
        if (sessions is not null && sessions.Judges(context))
        {
            return await sessions.OutstandingAsync(context, gate, cancellationToken).ConfigureAwait(false);
        }

        // AUTH-STEP-003: nothing of the library's reports what this caller proved, so
        // the gate is unmet, and the code tells a deployment that can ask for step-up
        // from one that cannot ask at all (LIB-HOST-004).
        return Error.From(assurance is null ? ErrorCodes.StepUpUnavailable : ErrorCodes.StepUpRequired);
    }
}
