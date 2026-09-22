using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Consents;

/// <summary>
/// Where the consent and objection records are.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-001, PRIV-RIGHT-001a and CONV-DESIGN-003. One record a
/// subject and purpose: withdrawal sets a timestamp on the record that is there and
/// a later consent replaces it, so the row is the history of one decision and never
/// a second opinion about the same one.
/// </remarks>
internal interface IConsentStore
{
    /// <summary>
    /// Every consent record one subject holds.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records, oldest first.</returns>
    ValueTask<IReadOnlyList<ConsentRecord>> ConsentsAsync(
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every objection record one subject holds.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records, oldest first.</returns>
    ValueTask<IReadOnlyList<ObjectionRecord>> ObjectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes a consent, replacing what the subject last decided about that purpose.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="consent">The record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask RecordAsync(
        SubjectId subject,
        ConsentRecord consent,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes an objection, replacing what the subject last decided about that
    /// purpose.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="objection">The record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask RecordAsync(
        SubjectId subject,
        ObjectionRecord objection,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every subject holding a live consent on one of the purposes named, recorded
    /// against a version other than the one named, which is the version just
    /// published and which nobody has been shown yet.
    /// </summary>
    /// <param name="purposes">The purposes the published document governs.</param>
    /// <param name="version">The version just published.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The consents and their holders, oldest first.</returns>
    ValueTask<IReadOnlyList<HeldConsent>> LiveAgainstAnotherAsync(
        IReadOnlyCollection<string> purposes,
        string version,
        CancellationToken cancellationToken);
}
