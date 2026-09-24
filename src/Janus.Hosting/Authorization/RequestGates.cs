using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Authorization.Gate;
using Janus.Core;
using Janus.Hosting.Bff;

namespace Janus.Hosting.Authorization;

/// <summary>
/// The step-up gates a host's action is bound to, judged against the session the
/// request arrived on.
/// </summary>
/// <param name="request">The session the request resolved to, where it resolved to one.</param>
/// <param name="guard">What judges a session against a gate under the person's policy.</param>
/// <remarks>
/// Implements AUTH-STEP-001, AUTH-STEP-002, AUTHZ-GATE-005 and D-160. Only the acting
/// person's own live session is judged: a context acting for someone else is judged by
/// the actor's session, and a context the request's session does not belong to is not
/// judged here at all. A gate is judged once a request, since a session proves more only
/// through a request of its own.
/// </remarks>
internal sealed class RequestGates(RequestSession request, StepUpGuard guard) : ISessionGates
{
    private readonly Dictionary<string, Error?> _judged = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool Judges(AccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context is { IsSystem: false, Acting: SubjectId acting }
            && request.Live is Session live
            && live.Subject == acting;
    }

    /// <inheritdoc/>
    public async ValueTask<Error?> OutstandingAsync(
        AccessContext context,
        string gate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gate);

        if (!Judges(context))
        {
            return Error.From(ErrorCodes.StepUpRequired);
        }

        if (_judged.TryGetValue(gate, out Error? judged))
        {
            return judged;
        }

        Session live = request.Required;

        // Chapter 09, POST /auth/step-up: the refusal carries what the gate costs and
        // what the person can present, as a gate on the library's own surface does.
        Error? outstanding = (await guard
                .ChallengeAsync(live.Subject, live.Id, gate, cancellationToken)
                .ConfigureAwait(false))
            .Match(
                challenge => StepUpRefusal.Met(challenge) ? null : StepUpRefusal.Of(gate, challenge),
                error => error);

        _judged[gate] = outstanding;

        return outstanding;
    }
}
