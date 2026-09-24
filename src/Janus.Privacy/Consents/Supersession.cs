using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Consents;

/// <summary>
/// What a material revision of a legal document does to the consents given on the
/// purposes that document governs, against an earlier version of it.
/// </summary>
/// <param name="consents">Where the records are.</param>
/// <param name="processing">What the deployment declared, which names the governing document.</param>
/// <param name="events">Where each ended consent is announced.</param>
/// <remarks>
/// Implements PRIV-CONS-007. Whether a version is material is the answer of the
/// person publishing it and never a judgement of the code's: a text edit that
/// superseded every live consent would stop a service on a paragraph.
/// Nothing here touches a purpose resting on another basis, because no consent record
/// exists for one (PRIV-SENS-002a).
/// </remarks>
internal sealed class Supersession(
    IConsentStore consents,
    DeclaredProcessing processing,
    IEvents events)
{
    /// <summary>
    /// Ends every live consent on the purposes the published document governs that
    /// was recorded against an earlier version of it, so that the subject is asked
    /// again.
    /// </summary>
    /// <param name="document">The document just published.</param>
    /// <param name="version">The version just published.</param>
    /// <param name="at">When it was published.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many consents it ended, or the failure where one was not announced.</returns>
    public async ValueTask<Result<int>> OfAsync(
        string document,
        string version,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        // A purpose that names no document is governed by the privacy notice, which
        // is what the declaration leaves unsaid (PRIV-CONS-001).
        string[] purposes =
        [
            .. processing.Purposes
                .Where(purpose => string.Equals(
                    purpose.Document ?? ConsentService.Notice,
                    document,
                    StringComparison.Ordinal))
                .Select(purpose => purpose.Name),
        ];

        if (purposes.Length is 0)
        {
            return Result.Success(0);
        }

        IReadOnlyList<HeldConsent> held = await consents
            .LiveAgainstAnotherAsync(purposes, version, cancellationToken)
            .ConfigureAwait(false);

        foreach (HeldConsent one in held)
        {
            await consents
                .RecordAsync(one.Subject, one.Consent with { SupersededAt = at }, cancellationToken)
                .ConfigureAwait(false);
            Result published = await events
                .PublishAsync(
                    new ConsentChanged(
                        at,
                        Key(one, at),
                        one.Consent.Purpose,
                        ConsentChange.Superseded)
                    {
                        Subject = one.Subject,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (published.Match(() => (Error?)null, error => error) is Error unpublished)
            {
                return Result.Failure<int>(unpublished);
            }
        }

        return Result.Success(held.Count);
    }

    private static string Key(HeldConsent one, DateTimeOffset at) =>
        one.Subject.ToString()
        + ":" + one.Consent.Purpose
        + ":Superseded@" + at.ToString("O", CultureInfo.InvariantCulture);
}
