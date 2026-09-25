using System;

namespace Janus.Core;

/// <summary>
/// One domain an organization locks its members' email addresses to, with the record
/// that proves it and where its verification stands.
/// </summary>
/// <param name="Domain">The domain, in its ASCII form.</param>
/// <param name="RecordName">Where the TXT record is published: <c>_identity-verify.</c> and the domain.</param>
/// <param name="RecordValue">What the record carries: <c>identity-domain-verification=</c> and the token.</param>
/// <param name="AddedAt">When the domain was listed.</param>
/// <param name="VerifiedAt">When the record was first found, or nothing while it never has been.</param>
/// <param name="CheckedAt">When the record was last looked for, or nothing while it never has been.</param>
/// <param name="LastCheckPassed">Whether that look found it, or nothing while there has been none.</param>
/// <remarks>
/// Implements REG-DOM-001, IDN-ORG-006 and chapter 09 section 8a. A listed domain admits
/// no address until it is verified; a failed re-verification alerts and leaves it
/// verified.
/// </remarks>
public sealed record OrganizationDomain(
    string Domain,
    string RecordName,
    string RecordValue,
    DateTimeOffset AddedAt,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? CheckedAt,
    bool? LastCheckPassed);
