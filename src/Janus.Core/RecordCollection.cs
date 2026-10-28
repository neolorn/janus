using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Janus.Core;

/// <summary>
/// What a host's own screen took from a person and presented to them, for the record a
/// collection writes.
/// </summary>
/// <param name="Organization">The organization the host keeps the data in.</param>
/// <param name="Language">One of the deployment's message languages, which every message to the record is sent in.</param>
/// <param name="Name">The name the person gave, where they gave one.</param>
/// <param name="NoticeVersion">The version of the privacy notice the screen presented.</param>
/// <param name="TermsVersion">The version of the terms the person accepted, where the host has terms.</param>
/// <param name="Consents">The consent-based purposes the person ticked, one control each.</param>
/// <param name="Contacts">
/// At most one email and one phone the screen took and did not verify, none of a kind
/// the collection session verified.
/// </param>
/// <param name="Reference">
/// What the person will hold of this collection (an order number, say), 1 to 64
/// characters after trimming, where there is one.
/// </param>
/// <remarks>Implements REC-COL-002, REC-REC-001 and PRIV-CONS-008a.</remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "LIB-API-005 and REC-COL-002 name what a collection takes a RecordCollection, and the type carries the name the specification uses.")]
public sealed record RecordCollection(
    OrganizationId Organization,
    string Language,
    string? Name,
    string NoticeVersion,
    string? TermsVersion,
    IReadOnlyList<string> Consents,
    IReadOnlyDictionary<IdentifierKind, string>? Contacts,
    string? Reference);
