using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// Whether a request is served on the calling flow, which is what tells a host's job
/// from a request: present for the whole of any request, through either profile or
/// neither and in every scope opened within it, and absent in background work
/// (IDN-PRIN-001, CONV-DESIGN-007, D-206).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class RequestServedTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");

    /// <summary>
    /// IDN-PRIN-001 AC6, CONV-DESIGN-007 (D-206): a request served through the browser
    /// profile, through the machine profile, or outside both is a request being served,
    /// in the request's own scope and in a scope opened within it.
    /// </summary>
    /// <param name="path">Where the request is sent.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(ServedRequests.Browser)]
    [InlineData(ServedRequests.Machine)]
    [InlineData(ServedRequests.Outside)]
    public async Task IDN_PRIN_001_AC6_ARequestIsServedThroughEitherProfileOrNeitherAndInAScopeWithinItAsync(
        string path)
    {
        bool? served = null;
        bool? within = null;

        await using ServedRequests deployment = await ServedRequests.StartAsync(
            host,
            async context =>
            {
                served = context.RequestServices.GetRequiredService<ISessionGates>().ServesRequest;

                await using AsyncServiceScope scope = context.RequestServices
                    .GetRequiredService<IServiceScopeFactory>()
                    .CreateAsyncScope();

                within = scope.ServiceProvider.GetRequiredService<ISessionGates>().ServesRequest;
            },
            TestContext.Current.CancellationToken);

        Answer answered = await deployment.SendAsync(path);

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.True(served);
        Assert.True(within);
    }

    /// <summary>
    /// IDN-PRIN-001 AC6, CONV-DESIGN-007 (D-206): background work in the same deployment,
    /// resolved outside any request, before a request and after one, serves none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_PRIN_001_AC6_BackgroundWorkServesNoRequestAsync()
    {
        await using ServedRequests deployment = await ServedRequests.StartAsync(
            host,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        bool before = await BackgroundAsync(deployment.Services);

        _ = await deployment.SendAsync(ServedRequests.Browser);

        Assert.False(before);
        Assert.False(await BackgroundAsync(deployment.Services));
    }

    /// <summary>
    /// IDN-PRIN-001 AC6, AUTHZ-TEST-001 AC2 (D-206): a job's list asked while a request
    /// is served is refused <c>authz.denied</c> with the identifier its refusal was
    /// recorded under, and the same list asked by background work is not refused for
    /// want of a request.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_PRIN_001_AC6_AJobsListIsRefusedInARequestAndNotInBackgroundWorkAsync()
    {
        var organization = OrganizationId.New(TimeProvider.System);
        Result<Expression<Func<HostDocument, bool>>>? inRequest = null;

        await using ServedRequests deployment = await ServedRequests.StartAsync(
            host,
            async context => inRequest = await FilteredAsync(context.RequestServices, organization),
            TestContext.Current.CancellationToken);

        _ = await deployment.SendAsync(ServedRequests.Browser);

        await using AsyncServiceScope background = deployment.Services.CreateAsyncScope();

        Result<Expression<Func<HostDocument, bool>>> outside =
            await FilteredAsync(background.ServiceProvider, organization);

        Assert.NotNull(inRequest);

        Error? refused = inRequest.Value.Match<Error?>(_ => null, error => error);

        Assert.Equal(ErrorCodes.Denied, refused?.Code);
        Assert.True(refused?.Details.ContainsKey("correlation"));
        Assert.True(outside.Match(_ => true, _ => false));
    }

    // Background work: a scope of the deployment's own, asked on a flow no request began.
    private static async Task<bool> BackgroundAsync(IServiceProvider deployment) =>
        await Task.Run(async () =>
        {
            await using AsyncServiceScope scope = deployment.CreateAsyncScope();

            return scope.ServiceProvider.GetRequiredService<ISessionGates>().ServesRequest;
        });

    // The host's list of documents under the context of its declared job.
    private async Task<Result<Expression<Func<HostDocument, bool>>>> FilteredAsync(
        IServiceProvider services,
        OrganizationId organization)
    {
        await using HostContext reading = host.Context();

        return await services.GetRequiredService<IAccessGate>().FilterAsync(
            AccessContext.Of(SystemPrincipal.ForOrganization(HostFixture.Digest, "the nightly digest", organization)),
            HostPermissions.Read,
            Document,
            organization,
            new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, document => document.Id)
                .Relationship("reviewer", reading.Reviewers),
            CancellationToken.None);
    }
}
