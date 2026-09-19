using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;

namespace Janus.Identity.Audit;

/// <summary>
/// One identity lifecycle event as it is recorded: who acted, whose identity the
/// action was taken under, when it happened, and the organization where one applies.
/// </summary>
/// <remarks>
/// Implements IDN-AUD-001, PRIV-RET-002, PRIV-RET-003, PRIV-RET-004 and IDN-PRIN-003.
/// The acting and effective identities are two fields where one would do, so that "who
/// did this" is never inferred. The record holds identifiers and codes; an attribute an
/// event has to carry is held under the subject's own key, so that erasure reaches it
/// without any row being touched.
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
        OrganizationId? organization,
        IReadOnlyDictionary<string, JsonElement> details,
        IReadOnlyDictionary<string, JsonElement> personalDetails)
    {
        Id = id;
        Category = category;
        Action = action;
        OccurredAt = occurredAt;
        ActingSubject = actingSubject;
        EffectiveSubject = effectiveSubject;
        Organization = organization;
        Details = details;
        PersonalDetails = personalDetails;
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
    /// Who took the action.
    /// </summary>
    public SubjectId ActingSubject { get; }

    /// <summary>
    /// Whose identity the action was taken under. It is the acting identity in every
    /// current path; the two fields are what makes an impersonated action legible.
    /// </summary>
    public SubjectId EffectiveSubject { get; }

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
    /// The attributes the event has to record, which the store holds under the
    /// effective subject's own key. Empty where the event records none.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> PersonalDetails { get; }

    /// <summary>
    /// Records an event.
    /// </summary>
    /// <param name="id">The identifier issued for the record.</param>
    /// <param name="category">Which retention it falls under.</param>
    /// <param name="action">What happened.</param>
    /// <param name="occurredAt">The instant it occurred.</param>
    /// <param name="actingSubject">Who took the action.</param>
    /// <param name="effectiveSubject">Whose identity it was taken under.</param>
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
        SubjectId effectiveSubject,
        OrganizationId? organization,
        IReadOnlyDictionary<string, JsonElement>? details = null,
        IReadOnlyDictionary<string, JsonElement>? personalDetails = null) =>
        new(
            id,
            category,
            action,
            occurredAt,
            actingSubject,
            effectiveSubject,
            organization,
            details ?? Nothing,
            personalDetails ?? Nothing);

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
    /// <param name="organization">The organization, where one applied.</param>
    /// <param name="details">The structured fields.</param>
    /// <param name="personalDetails">The attributes that were held under the key.</param>
    /// <returns>The record.</returns>
    /// <exception cref="ArgumentNullException">Either set of fields is absent.</exception>
    public static AuditRecord Existing(
        AuditRecordId id,
        AuditCategory category,
        AuditAction action,
        DateTimeOffset occurredAt,
        SubjectId actingSubject,
        SubjectId effectiveSubject,
        OrganizationId? organization,
        IReadOnlyDictionary<string, JsonElement> details,
        IReadOnlyDictionary<string, JsonElement> personalDetails)
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
            organization,
            details,
            personalDetails);
    }
}
