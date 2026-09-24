using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// One domain an organization lists in its lock, with the token its TXT record must
/// carry and where its verification stands.
/// </summary>
/// <remarks>
/// Implements REG-DOM-001 and IDN-ORG-006. A removal leaves the row where it is, so the
/// token it was drawn with is never drawn again and an address in the domain stays
/// refused until the domain is listed anew.
/// </remarks>
internal sealed class LockedDomain
{
    private const string RecordPrefix = "_identity-verify.";

    private const string ValuePrefix = "identity-domain-verification=";

    private LockedDomain(OrganizationId organization, string domain, [NeverLogged] string token, DateTimeOffset addedAt)
    {
        Organization = organization;
        Domain = domain;
        Token = token;
        AddedAt = addedAt;
    }

    /// <summary>
    /// Whose lock it is in.
    /// </summary>
    public OrganizationId Organization { get; }

    /// <summary>
    /// The domain, in its ASCII form.
    /// </summary>
    public string Domain { get; }

    /// <summary>
    /// The 32 random bytes the record carries, base64url.
    /// </summary>
    [NeverLogged]
    public string Token { get; }

    /// <summary>
    /// When it was listed.
    /// </summary>
    public DateTimeOffset AddedAt { get; }

    /// <summary>
    /// When the record was first found, where it has been.
    /// </summary>
    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>
    /// When the record was last looked for, where it has been.
    /// </summary>
    public DateTimeOffset? CheckedAt { get; private set; }

    /// <summary>
    /// Whether that look found it, where there has been one.
    /// </summary>
    public bool? LastCheckPassed { get; private set; }

    /// <summary>
    /// When it was removed from the lock, where it has been.
    /// </summary>
    public DateTimeOffset? RemovedAt { get; private set; }

    /// <summary>
    /// Whether the lock lists it.
    /// </summary>
    public bool IsListed => RemovedAt is null;

    /// <summary>
    /// Whether it admits addresses: listed and verified.
    /// </summary>
    public bool Admits => IsListed && VerifiedAt is not null;

    /// <summary>
    /// Where the record is published.
    /// </summary>
    public string RecordName => RecordPrefix + Domain;

    /// <summary>
    /// What the record carries.
    /// </summary>
    public string RecordValue => ValuePrefix + Token;

    /// <summary>
    /// A newly listed domain, unverified, with a token of its own.
    /// </summary>
    /// <param name="organization">Whose lock it is in.</param>
    /// <param name="domain">The domain, in its ASCII form.</param>
    /// <param name="randomness">Where the token is drawn from.</param>
    /// <param name="at">When it is listed.</param>
    /// <returns>The domain.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static LockedDomain Listed(
        OrganizationId organization,
        string domain,
        RandomNumberGenerator randomness,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return new LockedDomain(organization, domain, OpaqueToken.Draw(randomness).Value, at);
    }

    /// <summary>
    /// The domain as it already stands. This is the store's translation of a stored
    /// row and no change an administrator made.
    /// </summary>
    /// <param name="organization">Whose lock it is in.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="token">Its token.</param>
    /// <param name="addedAt">When it was listed.</param>
    /// <param name="verifiedAt">When it was verified, where it was.</param>
    /// <param name="checkedAt">When it was last checked, where it was.</param>
    /// <param name="lastCheckPassed">Whether that check passed, where there was one.</param>
    /// <param name="removedAt">When it was removed, where it was.</param>
    /// <returns>The domain.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static LockedDomain Existing(
        OrganizationId organization,
        string domain,
        [NeverLogged] string token,
        DateTimeOffset addedAt,
        DateTimeOffset? verifiedAt,
        DateTimeOffset? checkedAt,
        bool? lastCheckPassed,
        DateTimeOffset? removedAt)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(token);

        return new LockedDomain(organization, domain, token, addedAt)
        {
            VerifiedAt = verifiedAt,
            CheckedAt = checkedAt,
            LastCheckPassed = lastCheckPassed,
            RemovedAt = removedAt,
        };
    }

    /// <summary>
    /// Whether the records published at <see cref="RecordName"/> carry this domain's
    /// token.
    /// </summary>
    /// <param name="records">What was read there.</param>
    /// <returns>Whether one of them is <see cref="RecordValue"/> exactly.</returns>
    /// <exception cref="ArgumentNullException">The records are absent.</exception>
    public bool IsProvedBy(IReadOnlyList<string> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        foreach (string record in records)
        {
            if (string.Equals(record, RecordValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Records one look for the record. The first that finds it verifies the domain; a
    /// later one that does not leaves it verified, since a failed re-verification
    /// revokes nothing by itself.
    /// </summary>
    /// <param name="passed">Whether the record was found.</param>
    /// <param name="at">When it was looked for.</param>
    /// <exception cref="InvalidOperationException">The domain has been removed.</exception>
    public void Checked(bool passed, DateTimeOffset at)
    {
        if (!IsListed)
        {
            throw new InvalidOperationException("A removed domain is not checked.");
        }

        CheckedAt = at;
        LastCheckPassed = passed;

        if (passed && VerifiedAt is null)
        {
            VerifiedAt = at;
        }
    }

    /// <summary>
    /// Removes the domain from the lock.
    /// </summary>
    /// <param name="at">When.</param>
    /// <exception cref="InvalidOperationException">It has been removed already.</exception>
    public void Remove(DateTimeOffset at)
    {
        if (!IsListed)
        {
            throw new InvalidOperationException("The domain has been removed already.");
        }

        RemovedAt = at;
    }

    /// <summary>
    /// The domain as the contract answers it.
    /// </summary>
    /// <returns>The answer.</returns>
    public OrganizationDomain Answered() =>
        new(Domain, RecordName, RecordValue, AddedAt, VerifiedAt, CheckedAt, LastCheckPassed);
}
