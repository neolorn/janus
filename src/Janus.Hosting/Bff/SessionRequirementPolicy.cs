using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Hosting.Bff;

/// <summary>
/// Stage 8 for a request that passed it before routing matched an endpoint: the
/// endpoint routing then selects is judged as the stage judges it, before it runs.
/// </summary>
/// <remarks>
/// Implements BFF-HOST-002 and BFF-ORDER-001 (D-209). Mounting needs nothing
/// security-relevant to be right (BFF-OWN-001), so a host whose routing comes after the
/// browser profile is held to stage 8 as one whose routing comes before it: each
/// endpoint the request may not reach is replaced among the candidates with the
/// profile's refusal, and the host's own middleware after its routing then runs for a
/// request no host endpoint will serve. The judgement is the stage's own, so the two
/// orders cannot answer one request two ways.
/// </remarks>
internal sealed class SessionRequirementPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    /// <summary>
    /// Last of the policies, so what is judged is what every other policy leaves to be
    /// selected.
    /// </summary>
    public override int Order => int.MaxValue;

    /// <summary>
    /// Whether any of the endpoints is one that needs a session.
    /// </summary>
    /// <param name="endpoints">The endpoints routing may select among.</param>
    /// <returns>Whether the policy judges them.</returns>
    /// <exception cref="ArgumentNullException">The endpoints are absent.</exception>
    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.Any(SessionRequired.Asks);
    }

    /// <summary>
    /// Replaces each candidate the request may not reach with the profile's refusal,
    /// where the request passed stage 8 before routing matched it.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="candidates">The endpoints routing may select among.</param>
    /// <returns>The work of judging them.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(candidates);

        // Where routing comes before the profile, stage 8 has yet to run, and judges the
        // endpoint itself once it does.
        if (httpContext.Features.Get<SessionRequirementDeferred>() is null)
        {
            return Task.CompletedTask;
        }

        RequestSession resolved = httpContext.RequestServices.GetRequiredService<RequestSession>();

        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates.IsValidCandidate(index)
                && SessionRequired.Refuses(resolved, candidates[index].Endpoint) is Error refusal)
            {
                candidates.ReplaceEndpoint(index, Refused(refusal), values: null);
            }
        }

        return Task.CompletedTask;
    }

    // The profile's refusal, standing where the endpoint was, which nothing of the host's
    // serves.
    private static Endpoint Refused(Error refusal) =>
        new(
            context => Refusal.WriteAsync(context, refusal, context.RequestAborted),
            EndpointMetadataCollection.Empty,
            displayName: null);
}
