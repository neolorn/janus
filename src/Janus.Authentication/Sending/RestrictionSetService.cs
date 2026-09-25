using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// The named restriction set as the administration interface reaches it: the
/// permission asked in the administrative organization, the step-up judged on the
/// caller's session, and the edit made through the one operation that makes it.
/// </summary>
/// <param name="scope">Whether the caller may administer the deployment.</param>
/// <param name="guard">What the session's proof amounts to against a gate.</param>
/// <param name="administration">Where the set is read, edited and granted under.</param>
/// <remarks>
/// Implements LIB-API-005, AUTH-ABUSE-004, AUTHZ-SCOPE-001 and chapter 10 section 2.1:
/// <c>restriction:edit</c> reads and edits, <c>restriction:grant</c> grants.
/// </remarks>
internal sealed class RestrictionSetService(
    AdministrativeScope scope,
    StepUpGuard guard,
    RestrictionAdministration administration) : IRestrictionSet
{
    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<Restriction>>> AllAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope.RefusedAsync(context, Permissions.RestrictionEdit, cancellationToken)
                .ConfigureAwait(false) is Error refused)
        {
            return Result.Failure<IReadOnlyList<Restriction>>(refused);
        }

        return await administration.AllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Restriction>> ReadAsync(
        AccessContext context,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        return (await AllAsync(context, cancellationToken).ConfigureAwait(false)).Match(
            declared => Named(declared, name) is Restriction restriction
                ? Result.Success(restriction)
                : Result.Failure<Restriction>(Unnamed()),
            Result.Failure<Restriction>);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> EditAsync(
        AccessContext context,
        SessionId session,
        Restriction replacement,
        string? reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(replacement);

        if (await scope.RefusedAsync(context, Permissions.RestrictionEdit, cancellationToken)
                .ConfigureAwait(false) is Error refused)
        {
            return Result.Failure(refused);
        }

        // Chapter 09 section 8: an empty bucket list is refused whatever it would
        // replace, before anything is asked of the session.
        if (Settings.Restrictions.Accept([replacement]).Match(_ => (Error?)null, error => error)
            is Error unaccepted)
        {
            return Result.Failure(unaccepted);
        }

        return await ChallengedAsync(
                context,
                session,
                StepUpAction.RestrictionEdit,
                (challenge, actor) => administration.EditAsync(
                    replacement.Name,
                    replacement,
                    reason,
                    challenge,
                    actor,
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> DeleteAsync(
        AccessContext context,
        SessionId session,
        string name,
        string? reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        Error? failure = null;

        IReadOnlyList<Restriction> declared = (await AllAsync(context, cancellationToken).ConfigureAwait(false))
            .Match(one => one, error => Held<IReadOnlyList<Restriction>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // Deleting what is not there changes nothing, and a change of nothing is not
        // one to announce or to write down.
        if (Named(declared, name) is null)
        {
            return Result.Failure(Unnamed());
        }

        return await ChallengedAsync(
                context,
                session,
                StepUpAction.RestrictionEdit,
                (challenge, actor) => administration.EditAsync(
                    name,
                    replacement: null,
                    reason,
                    challenge,
                    actor,
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> GrantAsync(
        AccessContext context,
        SessionId session,
        string name,
        string keyValue,
        int credit,
        string? reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(keyValue);

        if (await scope.RefusedAsync(context, Permissions.RestrictionGrant, cancellationToken)
                .ConfigureAwait(false) is Error refused)
        {
            return Result.Failure(refused);
        }

        return await ChallengedAsync(
                context,
                session,
                StepUpAction.RestrictionGrant,
                (challenge, actor) => administration.GrantAsync(
                    name,
                    keyValue,
                    credit,
                    reason,
                    challenge,
                    actor,
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static Restriction? Named(IReadOnlyList<Restriction> declared, string name) =>
        declared.FirstOrDefault(one => string.Equals(one.Name, name, StringComparison.Ordinal));

    private static Error Unnamed() =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement("name"));

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // OPS-CFG-005: an edit and a grant answer for themselves through the person who
    // made them, and the gate is judged on that person's own session.
    private async ValueTask<Result> ChallengedAsync(
        AccessContext context,
        SessionId session,
        StepUpAction action,
        Func<StepUpChallenge, SubjectId, ValueTask<Result>> operation,
        CancellationToken cancellationToken)
    {
        if (context.Acting is not SubjectId actor)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        Error? failure = null;

        StepUpChallenge challenge = (await guard
                .ChallengeAsync(actor, session, action, cancellationToken)
                .ConfigureAwait(false))
            .Match(one => one, error => Held<StepUpChallenge>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        return await operation(challenge, actor).ConfigureAwait(false);
    }
}
