using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Consents;

/// <summary>
/// What a material revision of the privacy notice does to the consents given against
/// an earlier version of it.
/// </summary>
/// <param name="consents">Where the records are.</param>
/// <param name="events">Where each ended consent is announced.</param>
/// <remarks>
/// Implements PRIV-CONS-007. Whether a version is material is the answer of the
/// person publishing it and never a judgement of the code's: a text edit that
/// superseded every customer's consent would stop a storefront on a paragraph.
/// Nothing here touches a purpose resting on another basis, because no consent record
/// exists for one (PRIV-SENS-002a).
/// </remarks>
internal sealed class Supersession(IConsentStore consents, IEvents events)
{
    /// <summary>
    /// Ends every live consent recorded against an earlier version of the notice, so
    /// that the subject is asked again.
    /// </summary>
    /// <param name="noticeVersion">The version just published.</param>
    /// <param name="at">When it was published.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many consents it ended.</returns>
    public async ValueTask<int> OfAsync(
        string noticeVersion,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<HeldConsent> held = await consents
            .LiveAgainstAnotherAsync(noticeVersion, cancellationToken)
            .ConfigureAwait(false);

        foreach (HeldConsent one in held)
        {
            await consents
                .RecordAsync(one.Subject, one.Consent with { SupersededAt = at }, cancellationToken)
                .ConfigureAwait(false);
            await events
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
        }

        return held.Count;
    }

    private static string Key(HeldConsent one, DateTimeOffset at) =>
        one.Subject.ToString()
        + ":" + one.Consent.Purpose
        + ":Superseded@" + at.ToString("O", CultureInfo.InvariantCulture);
}
