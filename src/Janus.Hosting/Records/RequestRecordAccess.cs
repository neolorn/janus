using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Records;

/// <summary>
/// The reach of the record access session the request's browser holds through the
/// pre-authentication cookie.
/// </summary>
/// <param name="access">The record access sessions.</param>
/// <param name="browser">The sessions the request's browser holds.</param>
/// <param name="requests">The request being served, where one is.</param>
/// <remarks>
/// Implements REC-ACC-003 and BFF-CSRF-005a. The session is read as the library's own
/// <c>/records/access</c> endpoints read it, so a browser that opened none, or holds a
/// sign-in session, holds none here either.
/// </remarks>
internal sealed class RequestRecordAccess(
    RecordAccessSessions access,
    RequestSession browser,
    IHttpContextAccessor requests) : IRecordAccess
{
    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<SubjectId>>> ReachedAsync(CancellationToken cancellationToken)
    {
        // REC-ACC-003 AC4: called outside a browser request it is a fault of the calling
        // code, since no browser could hold the session that admits it.
        if (requests.HttpContext is null)
        {
            throw new InvalidOperationException("The reach of a record access session is read only while a browser's request is served.");
        }

        return await access.ReachedAsync(browser.Access, cancellationToken).ConfigureAwait(false);
    }
}
