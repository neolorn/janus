using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Records;

/// <summary>
/// The collection of a person record from the request being served: the collection
/// session its browser holds through the pre-authentication cookie admits it.
/// </summary>
/// <param name="collections">The collection itself.</param>
/// <param name="browser">The sessions the request's browser holds.</param>
/// <param name="requests">The request being served, where one is.</param>
/// <remarks>
/// Implements REC-COL-002 and BFF-CSRF-005a. The session is read as the library's own
/// collection endpoints read it, so a browser that began no collection, or holds a
/// sign-in session, holds none here either.
/// </remarks>
internal sealed class RequestRecordCollection(
    RecordCollectionService collections,
    RequestSession browser,
    IHttpContextAccessor requests) : IRecordCollection
{
    /// <inheritdoc/>
    public async ValueTask<Result<CollectedRecord>> CollectAsync(
        RecordCollection collection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collection);

        // REC-COL-002: called outside a browser request it is a fault of the calling
        // code, since no browser could hold the session that admits it.
        if (requests.HttpContext is null)
        {
            throw new InvalidOperationException("A person record is collected only while a browser's request is served.");
        }

        return await collections
            .CollectAsync(
                browser.Collection,
                collection,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
