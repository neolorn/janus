using System;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// A change made by an account restricted after the change's gate step and before its
/// unit of work begins, as a restriction committed between the two is
/// (AUTHZ-GATE-006 AC3).
/// </summary>
internal static class RestrictedSinceTheGateStep
{
    /// <summary>
    /// Runs the change with the account the gate admits it for restricted in the moment
    /// before the next unit of work begins, and holds that it is refused
    /// <c>authz.restricted</c>, that the unit of work it began is rolled back and that
    /// none is left open. The restriction is lifted once the change has answered.
    /// </summary>
    /// <param name="deployment">The deployment the change is made in.</param>
    /// <param name="change">The change.</param>
    /// <returns>The work of the check.</returns>
    public static async Task RefusesAsync(Deployment deployment, Func<Task<Answer>> change)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(change);

        SubjectId? restricted = null;
        int rolledBack = deployment.Work.RolledBack;

        // The first account admitted is the one the change acts for; whoever a later
        // step asks about is left as they stand.
        Action<SubjectId> admits = admitted =>
        {
            deployment.Gate.Admitted = null;
            deployment.Restriction.Admitted = null;
            deployment.Work.Meanwhile = () =>
            {
                restricted = admitted;
                deployment.Gate.Restrict(admitted);
                deployment.Restriction.Restrict(admitted);
            };
        };

        deployment.Gate.Admitted = admits;
        deployment.Restriction.Admitted = admits;

        Answer refused = await change();

        deployment.Gate.Admitted = null;
        deployment.Restriction.Admitted = null;
        deployment.Work.Meanwhile = null;

        SubjectId lifted = Assert.IsType<SubjectId>(restricted);

        deployment.Gate.Lift(lifted);
        deployment.Restriction.Lift(lifted);

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Restricted.ToString(), refused.Text("code"));
        Assert.Equal(rolledBack + 1, deployment.Work.RolledBack);
        Assert.False(deployment.Work.Open);
    }
}
