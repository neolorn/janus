using Janus.Authorization.Model;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// What the step-up gate bound to an action still asks of the caller's session.
/// </summary>
/// <param name="model">The host's declaration, read for the gate an action is bound to.</param>
/// <param name="assurance">
/// Where how far the caller's session has authenticated is read, which a deployment
/// consuming authorization without this library's authentication supplies, and which a
/// deployment requiring no step-up leaves absent.
/// </param>
/// <remarks>
/// Implements AUTHZ-GATE-005, AUTH-STEP-003 and LIB-HOST-004. An action bound to no
/// gate asks nothing of the session; a bound one is met by what the session has proved
/// and by nothing else.
/// </remarks>
internal sealed class StepUpGates(AuthorizationModel model, IAssuranceProvider? assurance)
{
    /// <summary>
    /// What the action's gate still requires, or nothing where it requires nothing.
    /// </summary>
    /// <param name="permission">The permission being exercised or offered.</param>
    /// <returns>The code a refusal carries, or nothing where nothing is outstanding.</returns>
    public ErrorCode? OutstandingOn(Permission permission)
    {
        if (model.GateOf(permission) is null)
        {
            return null;
        }

        // AUTH-STEP-002: a gate is three values the principal's policy sets, and a
        // session meets it by having proved them. A bound action is therefore refused
        // until it is shown met, and the code tells a deployment that can ask for
        // step-up from one that cannot ask at all (AUTH-STEP-003, LIB-HOST-004).
        return assurance is null ? ErrorCodes.StepUpUnavailable : ErrorCodes.StepUpRequired;
    }
}
