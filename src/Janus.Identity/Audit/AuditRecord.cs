using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;

namespace Janus.Identity.Audit;

/// <summary>
/// One identity lifecycle event as it is recorded: who acted, whose identity the
/// action was taken under, the data subject it concerns, when it happened, and the
/// organization where one applies.
/// </summary>
/// <remarks>
/// Implements IDN-AUD-001, AUTHZ-IMP-001, PRIV-BREACH-002, PRIV-RET-002, PRIV-RET-003,
/// PRIV-RET-004, IDN-PRIN-001, IDN-PRIN-003 and INF-BG-002 (D-166, 303). The acting and
/// effective identities are two fields where one would do, so that "who did this" is
/// never inferred; every new record carries the acting identity as the effective one,
/// and the account an action was taken on as its subject. An action of background work
/// names the system principal that took it and the reason it stated, in place of an
/// acting identity it does not have. An action a person took in a break-glass session
/// carries the reason given at the credential's use, in a field of its own, handed in
/// with the acting identity (OPS-BOOT-002, D-170). The record holds identifiers and codes; an
/// attribute an event has to carry is held under the subject's own key, so that erasure
/// reaches it without any row being touched.
/// </remarks>
internal sealed class AuditRecord
{
    private static readonly IReadOnlyDictionary<string, JsonElement> Nothing =
        new Dictionary<string, JsonElement>(capacity: 0, StringComparer.Ordinal);

    private AuditRecord(
        AuditRecordId id,
        AuditCategory category,
        AuditAction action,
        DateTimeOffset occurredAt,
        SubjectId actingSubject,
        SubjectId effectiveSubject,
        SubjectId? subject,
        OrganizationId? organization,
        IReadOnlyDictionary<string, JsonElement> details,
        IReadOnlyDictionary<string, JsonElement> personalDetails,
        string? principal,
        string? reason,
        string? breakGlassReason)
    {
        Id = id;
        Category = category;
        Action = action;
        OccurredAt = occurredAt;
        ActingSubject = actingSubject;
        EffectiveSubject = effectiveSubject;
        Subject = subject;
        Organization = organization;
        Details = details;
        PersonalDetails = personalDetails;
        Principal = principal;
        Reason = reason;
        BreakGlassReason = breakGlassReason;
    }

    /// <summary>
    /// The record's own identifier.
    /// </summary>
    public AuditRecordId Id { get; }

    /// <summary>
    /// Which retention the record falls under.
    /// </summary>
    public AuditCategory Category { get; }

    /// <summary>
    /// What happened, as a code.
    /// </summary>
    public AuditAction Action { get; }

    /// <summary>
    /// The instant the event occurred, which is never the instant it was written.
    /// </summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Who took the action, or the empty identifier where a system principal took it.
    /// </summary>
    public SubjectId ActingSubject { get; }

    /// <summary>
    /// Whose identity the action was taken under. It is the acting identity on every
    /// record written; the two fields are what makes an impersonated action legible.
    /// </summary>
    public SubjectId EffectiveSubject { get; }

    /// <summary>
    /// The data subject the record concerns, the account an action was taken on whoever
    /// took it, or nothing where it concerns none. A record written before the field
    /// existed names the account as its effective identity and has none.
    /// </summary>
    public SubjectId? Subject { get; }

    /// <summary>
    /// The organization the event belongs to, where one applies. Its absence on an
    /// event about a principal holding no membership is the recorded fact and no
    /// omission.
    /// </summary>
    public OrganizationId? Organization { get; }

    /// <summary>
    /// The structured fields of the event: identifiers and codes, never a rendered
    /// sentence and never a personal attribute.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Details { get; }

    /// <summary>
    /// The attributes the event has to record, which the store holds under the data
    /// subject's own key. Empty where the event records none.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> PersonalDetails { get; }

    /// <summary>
    /// The name of the system principal that took the action, or nothing where a person
    /// took it.
    /// </summary>
    public string? Principal { get; }

    /// <summary>
    /// The reason the system principal stated, or nothing where a person took the
    /// action.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// The reason given at the use of the break-glass credential, where a person took
    /// the action in the session it opened, or nothing.
    /// </summary>
    public string? BreakGlassReason { get; }

    /// <summary>
    /// Records an event.
    /// </summary>
    /// <param name="id">The identifier issued for the record.</param>
    /// <param name="category">Which retention it falls under.</param>
    /// <param name="action">What happened.</param>
    /// <param name="occurredAt">The instant it occurred.</param>
    /// <param name="actingSubject">Who took the action, under whose identity it was taken.</param>
    /// <param name="subject">
    /// The data subject it concerns: the account it was taken on, the actor's own
    /// included, or nothing where it concerns none.
    /// </param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the action was
    /// taken in the session it opened, or nothing.
    /// </param>
    /// <param name="organization">The organization, where one applies.</param>
    /// <param name="details">The structured fields, or nothing.</param>
    /// <param name="personalDetails">The attributes to hold under the key, or nothing.</param>
    /// <returns>The record.</returns>
    public static AuditRecord Of(
        AuditRecordId id,
        AuditCategory category,
        AuditAction action,
        DateTimeOffset occurredAt,
        SubjectId actingSubject,
        SubjectId? subject,
        string? breakGlassReason,
        OrganizationId? organization,
        IReadOnlyDictionary<string, JsonElement>? details = null,
        IReadOnlyDictionary<string, JsonElement>? personalDetails = null) =>
        new(
            id,
            category,
            action,
            occurredAt,
            actingSubject,
            actingSubject,
            subject,
            organization,
            details ?? Nothing,
            personalDetails ?? Nothing,
            principal: null,
            reason: null,
            breakGlassReason);

    /// <summary>
    /// Records an action background work took, under the name and the reason of the
    /// system principal it ran as.
    /// </summary>
    /// <param name="id">The identifier issued for the record.</param>
    /// <param name="category">Which retention it falls under.</param>
    /// <param name="action">What happened.</param>
    /// <param name="occurredAt">The instant it occurred.</param>
    /// <param name="principal">The system principal that took it.</param>
    /// <param name="subject">Whose account it was taken on, where one.</param>
    /// <param name="organization">The organization, where one applies.</param>
    /// <param name="details">The structured fields, or nothing.</param>
    /// <returns>The record.</returns>
    /// <exception cref="ArgumentNullException">The principal is absent.</exception>
    public static AuditRecord Of(
        AuditRecordId id,
        AuditCategory category,
        AuditAction action,
        DateTimeOffset occurredAt,
        SystemPrincipal principal,
        SubjectId? subject,
        OrganizationId? organization,
        IReadOnlyDictionary<string, JsonElement>? details = null)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return new AuditRecord(
            id,
            category,
            action,
            occurredAt,
            actingSubject: default,
            effectiveSubject: default,
            subject,
            organization,
            details ?? Nothing,
            Nothing,
            principal.Name,
            principal.Reason,
            breakGlassReason: null);
    }

    /// <summary>
    /// The record as it already stands. This is the store's translation of a stored row
    /// and no event that just happened.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="category">Which retention it falls under.</param>
    /// <param name="action">What happened.</param>
    /// <param name="occurredAt">The instant it occurred.</param>
    /// <param name="actingSubject">Who took the action.</param>
    /// <param name="effectiveSubject">Whose identity it was taken under.</param>
    /// <param name="subject">The data subject it concerns, where it named one.</param>
    /// <param name="organization">The organization, where one applied.</param>
    /// <param name="details">The structured fields.</param>
    /// <param name="personalDetails">The attributes that were held under the key.</param>
    /// <param name="principal">The system principal that took it, where one did.</param>
    /// <param name="reason">The reason that principal stated, where one did.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the action was
    /// taken in the session it opened.
    /// </param>
    /// <returns>The record.</returns>
    /// <exception cref="ArgumentNullException">Either set of fields is absent.</exception>
    public static AuditRecord Existing(
        AuditRecordId id,
        AuditCategory category,
        AuditAction action,
        DateTimeOffset occurredAt,
        SubjectId actingSubject,
        SubjectId effectiveSubject,
        SubjectId? subject,
        OrganizationId? organization,
        IReadOnlyDictionary<string, JsonElement> details,
        IReadOnlyDictionary<string, JsonElement> personalDetails,
        string? principal,
        string? reason,
        string? breakGlassReason)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(personalDetails);

        return new AuditRecord(
            id,
            category,
            action,
            occurredAt,
            actingSubject,
            effectiveSubject,
            subject,
            organization,
            details,
            personalDetails,
            principal,
            reason,
            breakGlassReason);
    }
}
