using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.BreakGlass;

/// <summary>
/// The break-glass credential called in process through the contract a host holds,
/// <see cref="IBreakGlass"/>, which asks what the endpoints ask (LIB-API-005,
/// OPS-BOOT-004, OPS-BOOT-001 AC3).
/// </summary>
[Trait("kind", "unit")]
public sealed class BreakGlassServiceTests : IAsyncDisposable
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment administered by one organization.
    /// </summary>
    public BreakGlassServiceTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// LIB-API-005 AC2 and OPS-BOOT-004: a generation called in process meets the
    /// checks the route meets, in the service: without <c>system:administer</c> it is
    /// refused, and with it but no recent proof it asks for a step-up; nothing is
    /// issued. The read of the standing credential asks for the permission alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_005_AC2_AnInProcessGenerationIsHeldToTheEndpointsChecksAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        _ = await Flow.SignedInAsync(_deployment);

        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        SessionId session = _deployment.Sessions.All.Last(held => held.Subject == subject).Id;

        await using AsyncServiceScope scope = _deployment.Scope();
        IBreakGlass breakGlass = scope.ServiceProvider.GetRequiredService<IBreakGlass>();

        Error? withheld = Refusal(await breakGlass.GenerateAsync(AccessContext.Of(subject), session, cancellationToken));
        Error? unread = Refusal(await breakGlass.StandingAsync(AccessContext.Of(subject), cancellationToken));

        _deployment.Gate.Grant(subject, Administration, Permissions.SystemAdminister);
        _deployment.Clock.Advance(TimeSpan.FromDays(1));

        Error? challenged = Refusal(await breakGlass.GenerateAsync(AccessContext.Of(subject), session, cancellationToken));
        DateTimeOffset? standing = (await breakGlass.StandingAsync(AccessContext.Of(subject), cancellationToken))
            .Match(value => value, error => throw new Xunit.Sdk.XunitException($"The read was refused: {error.Code}."));

        Assert.Equal(ErrorCodes.Denied, withheld?.Code);
        Assert.Equal(ErrorCodes.Denied, unread?.Code);
        Assert.Equal(ErrorCodes.StepUpRequired, challenged?.Code);
        Assert.Null(standing);
        Assert.Empty(_deployment.BreakGlass.Issues);
    }

    private static Error? Refusal<TValue>(Result<TValue> outcome) =>
        outcome.Match(_ => (Error?)null, error => error);
}
