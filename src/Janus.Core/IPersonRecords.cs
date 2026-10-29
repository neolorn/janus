using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The operations over person records: what staff do to a record of someone who holds
/// no account, and what an account does to attach the records a proof offers it.
/// </summary>
/// <remarks>
/// Implements LIB-API-005 and chapter 21. Each is asked of the access gate, and a
/// refusal keeps nothing, so each may be called inside the host's unit of work.
/// </remarks>
public interface IPersonRecords
{
    /// <summary>
    /// Enters a record of origin <c>staff</c> for someone who gave their details
    /// otherwise, under <c>record:create</c> in the organization the entry names. Its
    /// contacts are held unverified, so no code reaches it and no account is offered it.
    /// </summary>
    /// <param name="context">Which staff member is entering it.</param>
    /// <param name="entry">What the person gave.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The record's subject, or the refusal: <c>authz.denied</c> where the caller does
    /// not hold the permission in the organization; <c>api.request.invalid</c> naming the
    /// member for an organization the deployment does not hold or whose deletion is
    /// requested, or a language it does not send in; <c>api.request.malformed</c> naming
    /// <c>reference</c> outside its bounds; <c>identity.identifier.invalid</c> for an
    /// email or phone that is not well formed; <c>identity.profile.invalid</c> for a name
    /// outside its rule; <c>privacy.purpose.noconsent</c> for a purpose that does not rest
    /// on consent; <c>privacy.notice.unpublished</c> for a consent whose governing
    /// document has no published version.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context or the entry is absent.</exception>
    ValueTask<Result<SubjectId>> EnterAsync(
        AccessContext context,
        PersonRecordEntry entry,
        CancellationToken cancellationToken);

    /// <summary>
    /// The person records an organization holds that hold an email or a phone, read in
    /// its canonical form, under <c>record:read</c> in that organization; asked in the
    /// administrative organization, those every organization holds.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">The organization the finding is asked in.</param>
    /// <param name="kind">Whether the value is an email or a phone.</param>
    /// <param name="value">The value, as entered.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The records' subjects, none where no record holds the value, or the refusal:
    /// <c>identity.organization.notfound</c> where the deployment holds no such
    /// organization or erased it, <c>authz.denied</c> where the caller does not hold the
    /// permission there.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context or the value is absent.</exception>
    ValueTask<Result<IReadOnlyList<SubjectId>>> FindAsync(
        AccessContext context,
        OrganizationId organization,
        IdentifierKind kind,
        string value,
        CancellationToken cancellationToken);

    /// <summary>
    /// The person records an organization holds that hold a reference, under
    /// <c>record:read</c> in that organization; asked in the administrative organization,
    /// those every organization holds.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">The organization the finding is asked in.</param>
    /// <param name="reference">The reference, as entered.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The records' subjects, none where no record holds the reference, or the refusal
    /// as the finding by a contact answers it.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context or the reference is absent.</exception>
    ValueTask<Result<IReadOnlyList<SubjectId>>> FindAsync(
        AccessContext context,
        OrganizationId organization,
        string reference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads a person record under <c>record:read</c>, asked in the organization that
    /// holds it and admitted where the permission is held there or in the administrative
    /// organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The record as staff read it, or the refusal: <c>identity.record.notfound</c>
    /// alike where no record bears the subject and where the caller is admitted in
    /// neither organization.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    ValueTask<Result<PersonRecordDetail>> ReadAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Corrects a person record on the person's word under <c>record:manage</c>, admitted
    /// as <see cref="ReadAsync"/> is: a contact changed is unverified from then on, so a
    /// code never reaches a contact staff wrote. A restricted record admits it.
    /// </summary>
    /// <param name="context">The staff member correcting it.</param>
    /// <param name="subject">The record's subject.</param>
    /// <param name="correction">What is corrected, and why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.record.notfound</c> as the read answers it;
    /// <c>api.request.malformed</c> naming <c>reason</c> where it is blank or past 1024
    /// characters after trimming; <c>identity.profile.invalid</c>,
    /// <c>api.request.invalid</c> naming <c>language</c> or
    /// <c>identity.identifier.invalid</c> for a member out of its rule;
    /// <c>identity.record.followsaccount</c> naming the account a record is attached to;
    /// <c>identity.record.stateconflict</c> where it is deleting or deleted.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context or the correction is absent.</exception>
    ValueTask<Result> RectifyAsync(
        AccessContext context,
        SubjectId subject,
        PersonRecordCorrection correction,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lifts a person record's restriction under <c>record:manage</c>, admitted as
    /// <see cref="ReadAsync"/> is, behind the step-up <c>record:restrictionlift</c>: the
    /// record returns active and <c>RestrictionChanged</c> is announced.
    /// </summary>
    /// <param name="context">The staff member lifting it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.record.notfound</c> as the read answers it;
    /// <c>identity.record.followsaccount</c> naming the account a record is attached to;
    /// <c>identity.record.stateconflict</c> where it is not restricted, one holding a
    /// restriction in its deletion window included; then the step-up's.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    ValueTask<Result> LiftRestrictionAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a person record's deletion begun by the person or by a request that
    /// arrived out of band under <c>record:manage</c>, admitted as <see cref="ReadAsync"/>
    /// is, behind the step-up <c>record:deletioncancel</c>, restoring the record as it
    /// stood.
    /// </summary>
    /// <param name="context">The staff member cancelling it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.record.notfound</c> as the read answers it;
    /// <c>identity.record.followsaccount</c> naming the account a record is attached to;
    /// <c>identity.takedown.active</c> where a takedown began it;
    /// <c>identity.record.stateconflict</c> where no window runs;
    /// <c>identity.deletion.windowelapsed</c> where it has closed; then the step-up's.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    ValueTask<Result> CancelDeletionAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Assembles a person record's export for staff to hand to the person who reaches it
    /// by no code, under <c>record:manage</c>, admitted as <see cref="ReadAsync"/> is,
    /// behind the step-up <c>record:export</c>: as the record access session's export
    /// answers it, counted against <c>privacy.export.ratelimit</c> for the record and
    /// recorded <c>privacy.export.assembled</c> with the staff member acting.
    /// </summary>
    /// <param name="context">The staff member assembling it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The export, or the refusal: <c>identity.record.notfound</c> as the read answers it;
    /// <c>identity.record.followsaccount</c> naming the account a record is attached to;
    /// <c>identity.record.stateconflict</c> where it is deleting or deleted; then the
    /// step-up's; <c>auth.throttled</c> where the limit is spent; <c>system.fault</c>
    /// naming a contributor that failed.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    ValueTask<Result<SubjectExport>> ExportAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// The subjects of the person records attached to an account: a person's own context
    /// reads its own; another's is read under <c>record:read</c> in the organization
    /// named, the administrative organization where none is, with the account reach of
    /// IDN-READ-004, and answers the attached records that organization holds, every one
    /// in the administrative organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="account">The account.</param>
    /// <param name="organization">The organization the read is asked in, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The subjects, in the order the records were attached, or the refusal:
    /// <c>identity.organization.notfound</c> where the deployment holds no organization
    /// named or erased it, <c>identity.account.notfound</c> where no account bears the
    /// subject or it lies outside the reach, the gate's refusal otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    ValueTask<Result<IReadOnlyList<SubjectId>>> AttachedAsync(
        AccessContext context,
        SubjectId account,
        OrganizationId? organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// The person records attached to the asking account.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The records, in the order they were attached, or <c>authz.denied</c> where the
    /// context is no person's.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    ValueTask<Result<IReadOnlyList<AttachedRecord>>> ListAttachedAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// What the latest proof of an email or phone on the session offers the account to
    /// attach: a count and its expiry, nothing of the records.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The offer, <see cref="RecordOffer.None"/> where none stands, or
    /// <c>authz.denied</c> where the context is no person's.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    ValueTask<Result<RecordOffer>> ReadOfferAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sends a code to one of the account's verified emails or phones, whose presentation
    /// proves it now and makes the offer. A code to a phone goes only where the phone
    /// signal does not answer risk, and the answer is the same either way; the signal is
    /// asked before any unit of work is open, so a code to a phone is not asked inside
    /// the host's.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="identifier">The identifier the code goes to.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.identifier.invalid</c> where the account
    /// holds no verified email or phone under the identifier; <c>auth.throttled</c>, or
    /// the governed send's refusal.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context or the source is absent.</exception>
    ValueTask<Result> SendFindCodeAsync(
        AccessContext context,
        SessionId session,
        IdentifierId identifier,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Proves by its code the identifier the last code on the session went to, and makes
    /// the offer it brings.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="code">The code entered.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The offer, or the refusal: <c>auth.code.invalid</c>, then <c>auth.code.expired</c>
    /// for a code past its life or its attempts and where no code is outstanding;
    /// <c>auth.throttled</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    ValueTask<Result<RecordOffer>> VerifyFindCodeAsync(
        AccessContext context,
        SessionId session,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attaches to the account each record the offer counted, each judged again under its
    /// lock, and ends the offer.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// How many were attached, none where no offer stands, or the refusal:
    /// <c>authz.restricted</c> while the account is restricted;
    /// <c>identity.account.stateconflict</c> where the account, read under its lock, is
    /// in any other state but active.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    ValueTask<Result<int>> AttachAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attaches to the account, inside the offer's life, each active record attached to
    /// no account that holds a value the offer proved and a reference, whatever the age
    /// of its verification. One that matches none is counted against the source and the
    /// account as a wrong code is.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="reference">The reference, trimmed and 1 to 64 characters long.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// How many were attached, none where none matched, or the refusal:
    /// <c>authz.restricted</c>; <c>identity.record.nooffer</c> where no offer stands;
    /// <c>auth.throttled</c>; <c>identity.account.stateconflict</c> where the account is
    /// not active.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    ValueTask<Result<int>> AttachByReferenceAsync(
        AccessContext context,
        SessionId session,
        string reference,
        string source,
        CancellationToken cancellationToken);
}
