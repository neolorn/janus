using System;
using Janus.Core;

namespace Janus.Hosting.Organizations;

/// <summary>
/// One domain in an organization's lock, as the response carries it: the record that
/// proves it, and where its verification stands.
/// </summary>
/// <param name="Domain">The domain, in its ASCII form.</param>
/// <param name="RecordName">Where the TXT record is published.</param>
/// <param name="RecordValue">What the record carries.</param>
/// <param name="AddedAt">When it was listed.</param>
/// <param name="VerifiedAt">When the record was first found, or nothing while it admits no address.</param>
/// <param name="CheckedAt">When the record was last looked for, or nothing.</param>
/// <param name="LastCheckPassed">Whether that look found it, or nothing.</param>
/// <remarks>Implements chapter 09 section 8a, REG-DOM-001 and API-CONV-002.</remarks>
internal sealed record OrganizationDomainView(
    string Domain,
    string RecordName,
    string RecordValue,
    DateTimeOffset AddedAt,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? CheckedAt,
    bool? LastCheckPassed)
{
    /// <summary>
    /// The view of one domain.
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The domain is absent.</exception>
    public static OrganizationDomainView Of(OrganizationDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return new(
            domain.Domain,
            domain.RecordName,
            domain.RecordValue,
            domain.AddedAt,
            domain.VerifiedAt,
            domain.CheckedAt,
            domain.LastCheckPassed);
    }
}
