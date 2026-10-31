using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Janus.Hosting.Tests.Sending;

/// <summary>
/// Where a message's outcome reaches the library: from the host in process, and through
/// no route of its own (INT-SMS-005, D-225).
/// </summary>
[Trait("kind", "unit")]
public sealed class OutcomeRouteTests : IAsyncDisposable
{
    private readonly Deployment _deployment = new();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// INT-SMS-005 AC4: no route the library mounts takes a gateway's report, and the
    /// machine profile governs none of the callbacks a gateway would post to.
    /// </summary>
    [Fact]
    public void INT_SMS_005_AC4_NoRouteOfTheLibraryTakesAGatewaysReport()
    {
        string[] routes =
        [
            .. _deployment.Endpoints
                .OfType<RouteEndpoint>()
                .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty),
        ];

        Assert.NotEmpty(routes);
        Assert.DoesNotContain(routes, route => route.StartsWith("/callbacks/sms", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            MachineRoutes.Paths,
            path => path.StartsWithSegments("/callbacks/sms", StringComparison.OrdinalIgnoreCase));
    }
}
