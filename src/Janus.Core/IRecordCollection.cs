using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The collection of a person record: what a host's own screen took from a person who
/// holds no account, written in the host's own unit of work.
/// </summary>
/// <remarks>
/// Implements REC-COL-002 and LIB-API-005. A seam, not an operation: it has no endpoint
/// and takes no access context, since the person holds none. What admits it is the
/// collection session the browser of the request being served holds, so it is called
/// while that request is served and inside the host's unit of work, and a refusal
/// writes nothing and keeps nothing.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "LIB-API-005 and REC-COL-002 name the seam IRecordCollection, and the type carries the name the specification uses.")]
public interface IRecordCollection
{
    /// <summary>
    /// Writes the record the browser's collection session verified the contacts of, or
    /// reuses the one of the organization that holds a contact it verified, and spends
    /// the session.
    /// </summary>
    /// <param name="collection">What the screen took and presented.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The record's subject and whether it was created, or the refusal:
    /// <c>identity.collection.expired</c> where the browser holds no collection session;
    /// <c>identity.collection.incomplete</c> where a contact the session staged is
    /// unverified, the age it asks is unanswered, or no contact is given at all;
    /// <c>privacy.consent.superseded</c> naming <c>noticeVersion</c> where it is not the
    /// privacy notice's current version, and <c>privacy.notice.unpublished</c> where none
    /// is published; <c>privacy.purpose.noconsent</c> for a purpose that does not rest
    /// on consent; <c>api.request.invalid</c> naming the member for an organization the
    /// deployment does not hold or whose deletion is requested, a language it does not
    /// send in, a contact of a kind the session verified or not well formed, or a
    /// reference outside its bounds; <c>identity.profile.invalid</c> for a name outside
    /// its rule.
    /// </returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    /// <exception cref="InvalidOperationException">No browser request is being served.</exception>
    ValueTask<Result<CollectedRecord>> CollectAsync(
        RecordCollection collection,
        CancellationToken cancellationToken);
}
