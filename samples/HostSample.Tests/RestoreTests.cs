using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// A staff session restored in place after an idle expiry, as the workshop's frontend
/// restores it: the request the expiry interrupted, the restore with the staff member's
/// passkey, and the same request again (LIB-TEST-004, D-224, D-228). The staff member is
/// the first administrator, whose administrative organization holds its sessions to
/// AAL2 (AUTH-SESS-005b), the floor under which the single-factor restore is offered.
/// The test moves the deployment's clock past the idle limit of every session it holds,
/// so it has a deployment of its own.
/// </summary>
/// <param name="host">The deployment.</param>
public sealed class RestoreTests(WorkshopHost host) : IClassFixture<WorkshopHost>
{
    // Past `session.aal2.inactivity`, one hour by default, and well inside the 24 hours
    // of `session.aal2.absolute`.
    private static readonly TimeSpan PastTheIdleLimit = TimeSpan.FromMinutes(61);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// AUTH-SESS-005 AC4, LIB-TEST-004: the first administrator's session, idle past its
    /// limit inside its absolute window, is refused with the single-factor restore
    /// offered, is restored in place with the administrator's passkey, and the request
    /// the expiry interrupted, the workshop's staff page, is then answered.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_005_AC4_AStaffSessionIsRestoredInPlaceAfterAnIdleExpiryAsync()
    {
        string route = "workshops/" + host.Workshop + "/staff";

        host.Clock.Advance(PastTheIdleLimit);

        Answered refused = await Exchanges.SendAsync(host.Administrator, HttpMethod.Get, route, null, Cancellation);

        await host.Administrator.RestoreAsync(Cancellation);

        Answered retried = await Exchanges.SendAsync(host.Administrator, HttpMethod.Get, route, null, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.Status);
        Assert.Equal("auth.session.expired", refused.Code);
        Assert.Equal(
            "single-factor",
            refused.Body!.Value.GetProperty("details").GetProperty("reauthenticate").GetString());
        Assert.Equal(HttpStatusCode.OK, retried.Status);
        Assert.Equal(JsonValueKind.Array, retried.Body!.Value.GetProperty("members").ValueKind);
    }
}
