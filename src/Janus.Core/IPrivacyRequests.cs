using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The data subject request queue: what a subject submits for themselves, what a
/// human enters for a request that arrived out of band, and the decision.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, PRIV-RIGHT-001, PRIV-RIGHT-002 and chapter 09 sections 7
/// and 8a. Erasure is not submitted here: a signed-in subject exercises it with
/// account deletion, and an erasure that arrives by letter or support mail is entered
/// by an authorised human who has confirmed the requester is the subject.
/// </remarks>
public interface IPrivacyRequests
{
    /// <summary>
    /// Submits the caller's own request, which is received today.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="type">What is asked for: restriction or rectification.</param>
    /// <param name="detail">What the subject wrote.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The receipt, or the refusal.</returns>
    ValueTask<Result<PrivacyRequestReceipt>> SubmitAsync(
        AccessContext context,
        PrivacyRequestType type,
        string detail,
        CancellationToken cancellationToken);

    /// <summary>
    /// Enters a request that reached the company out of band, on a subject's behalf.
    /// </summary>
    /// <param name="context">Which authorised human is entering it.</param>
    /// <param name="entry">The request as it arrived.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The receipt, or the refusal: <c>privacy.request.receivedfuture</c> for a date
    /// later than today in the deployment's zone, <c>privacy.request.duplicate</c>
    /// where an identical request is already open.
    /// </returns>
    ValueTask<Result<PrivacyRequestReceipt>> EnterAsync(
        AccessContext context,
        PrivacyRequestEntry entry,
        CancellationToken cancellationToken);

    /// <summary>
    /// The queue, each request with its deadline and status.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The requests, oldest first, or the refusal.</returns>
    ValueTask<Result<IReadOnlyList<PrivacyRequest>>> QueueAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Decides to do what was asked.
    /// </summary>
    /// <param name="context">Who is deciding.</param>
    /// <param name="request">Which request.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal.</returns>
    ValueTask<Result> FulfilAsync(
        AccessContext context,
        PrivacyRequestId request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Decides not to, and records why.
    /// </summary>
    /// <param name="context">Who is deciding.</param>
    /// <param name="request">Which request.</param>
    /// <param name="reason">Why, in the words of the person deciding.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal.</returns>
    ValueTask<Result> RefuseAsync(
        AccessContext context,
        PrivacyRequestId request,
        string reason,
        CancellationToken cancellationToken);
}
