using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Consents;

/// <summary>
/// Where the consent and objection records are.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-001, PRIV-RIGHT-001a and CONV-DESIGN-003. A record a grant
/// and a record an objection: a later one is added beside the earlier, what ends one
/// is stamped onto it, and nothing here overwrites or removes a record. A subject
/// holds at most one live record a purpose, so an addition answers whether it was
/// made.
/// </remarks>
internal interface IConsentStore
{
    /// <summary>
    /// Holds one subject's consents and objections against every other change of them
    /// until the operation's transaction ends, so what the subject holds is read as
    /// committed and two changes at once are made one after the other
    /// (CONV-DESIGN-003).
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>The work of holding them.</returns>
    /// <exception cref="System.InvalidOperationException">No transaction is open.</exception>
    ValueTask HoldAsync(SubjectId subject, CancellationToken cancellationToken);

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
    /// Adds a consent as a record of its own, where the subject holds no live record
    /// for its purpose.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="consent">The record, live.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether it was added: <see langword="false"/> where a live record for the
    /// purpose stands, whoever wrote it and however lately, and nothing was written.
    /// </returns>
    ValueTask<bool> AddAsync(
        SubjectId subject,
        ConsentRecord consent,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds an objection as a record of its own, where the subject holds no standing
    /// objection to its purpose.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="objection">The record, standing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether it was added: <see langword="false"/> where an objection to the purpose
    /// stands, whoever wrote it and however lately, and nothing was written.
    /// </returns>
    ValueTask<bool> AddAsync(
        SubjectId subject,
        ObjectionRecord objection,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stamps as withdrawn the record that stands for the subject's consent to one
    /// purpose: the live one, or the latest where none is live, a superseded consent
    /// being the subject's to take back too.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="at">When it was taken back.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether a record was stamped: <see langword="false"/> where the subject holds
    /// none for the purpose or it is withdrawn already.
    /// </returns>
    ValueTask<bool> WithdrawConsentAsync(
        SubjectId subject,
        string purpose,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stamps as superseded the subject's live consent to one purpose.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="at">When it was ended.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether a record was stamped: <see langword="false"/> where no live record for
    /// the purpose stands.
    /// </returns>
    ValueTask<bool> SupersedeAsync(
        SubjectId subject,
        string purpose,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stamps as withdrawn the subject's standing objection to one purpose.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="at">When it was taken back.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether a record was stamped: <see langword="false"/> where no objection to the
    /// purpose stands.
    /// </returns>
    ValueTask<bool> WithdrawObjectionAsync(
        SubjectId subject,
        string purpose,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every subject holding a live consent on one of the purposes named, recorded
    /// against another document or another version than the ones named, which are the
    /// document and version just published and which nobody has been shown yet.
    /// </summary>
    /// <param name="purposes">The purposes the published document governs.</param>
    /// <param name="document">The document just published.</param>
    /// <param name="version">The version just published.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The consents and their holders, oldest first.</returns>
    ValueTask<IReadOnlyList<HeldConsent>> LiveAgainstAnotherAsync(
        IReadOnlyCollection<string> purposes,
        string document,
        string version,
        CancellationToken cancellationToken);
}
