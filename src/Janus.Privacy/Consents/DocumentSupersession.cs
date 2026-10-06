using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Consents;

/// <summary>
/// What a start does to the consents recorded against a document their purpose no
/// longer names: each is stamped superseded and announced, so the subject is asked
/// again.
/// </summary>
/// <param name="consents">Where the records are.</param>
/// <param name="processing">What the deployment declared, which names the governing document.</param>
/// <param name="events">Where each ended consent is announced.</param>
/// <param name="work">The one transaction the stamp and the announcements run in.</param>
/// <param name="time">The clock the stamp is read from.</param>
/// <remarks>
/// Implements PRIV-CONS-007 (D-183). The declaration lives in the process, so a move
/// of a purpose to another document is learned of as the process starts. The stamp is
/// one conditional statement that answers which consents it ended, so of two starts
/// each consent is stamped and announced by one. Nothing here touches a purpose
/// resting on another basis, because no consent record exists for one
/// (PRIV-SENS-002a).
/// </remarks>
internal sealed class DocumentSupersession(
    IConsentStore consents,
    DeclaredProcessing processing,
    IEvents events,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// Ends every live consent of a consent-based purpose that was recorded against
    /// another document than the one the purpose now names.
    /// </summary>
    /// <param name="cancellationToken">Abandons the start.</param>
    /// <returns>How many consents it ended.</returns>
    /// <exception cref="InvalidOperationException">
    /// The transaction could not begin or commit, or a consent was not announced, which
    /// is a fault naming the code.
    /// </exception>
    public async ValueTask<int> SupersededAsync(CancellationToken cancellationToken)
    {
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (DeclaredPurpose purpose in processing.Purposes)
        {
            // A purpose that names no document is governed by the privacy notice, which
            // is what the declaration leaves unsaid (PRIV-CONS-001).
            if (purpose.Consent is not null)
            {
                documents[purpose.Name] = purpose.Document ?? ConsentService.Notice;
            }
        }

        if (documents.Count is 0)
        {
            return 0;
        }

        DateTimeOffset at = time.GetUtcNow();

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(_ => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        IReadOnlyList<EndedConsent> ended = await consents
            .SupersedeAgainstAnotherAsync(documents, at, cancellationToken)
            .ConfigureAwait(false);

        foreach (EndedConsent one in ended)
        {
            Result published = await events
                .PublishAsync(
                    new ConsentChanged(
                        at,
                        Supersession.Key(one.Subject, one.Purpose, at),
                        one.Purpose,
                        ConsentChange.Superseded)
                    {
                        Subject = one.Subject,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (published.Match(() => (Error?)null, error => error) is Error unpublished)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                throw new InvalidOperationException(unpublished.Code.ToString());
            }
        }

        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        return ended.Count;
    }
}
