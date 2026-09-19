using System;
using Janus.Core;

namespace Janus.Identity.Profiles;

/// <summary>
/// The four fields of an account's profile, less the photo, which is held apart so that
/// an ordinary read of the profile never carries image bytes.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-007 and REG-PROF-001. Every field here is optional: identity
/// does no proofing, so it needs no name. The legal name and the date of birth are off
/// unless the deployment turns them on, and the date is the one entered at the age step
/// rather than one the person edits afterwards.
/// </remarks>
internal sealed class Profile
{
    private Profile(SubjectId subject, DisplayName? displayName, LegalName? legalName, DateOnly? dateOfBirth)
    {
        Subject = subject;
        DisplayName = displayName;
        LegalName = legalName;
        DateOfBirth = dateOfBirth;
    }

    /// <summary>
    /// Whose profile it is.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// The name shown where a human needs to know who the account is. Where it is
    /// absent, the primary email is shown instead, which is the caller's business.
    /// </summary>
    public DisplayName? DisplayName { get; private set; }

    /// <summary>
    /// The name a host that invoices or ships collects.
    /// </summary>
    public LegalName? LegalName { get; private set; }

    /// <summary>
    /// The date entered at the age step, where the deployment retains it.
    /// </summary>
    public DateOnly? DateOfBirth { get; private set; }

    /// <summary>
    /// Whether the profile carries anything at all.
    /// </summary>
    public bool IsEmpty => DisplayName is null && LegalName is null && DateOfBirth is null;

    /// <summary>
    /// The profile of an account that has filled nothing in.
    /// </summary>
    /// <param name="subject">Whose profile it is.</param>
    /// <returns>The profile.</returns>
    public static Profile Empty(SubjectId subject) => new(subject, null, null, null);

    /// <summary>
    /// The profile as it already stands. This is the store's translation of a stored
    /// row and no edit.
    /// </summary>
    /// <param name="subject">Whose profile it is.</param>
    /// <param name="displayName">The display name, where one is held.</param>
    /// <param name="legalName">The legal name, where one is held.</param>
    /// <param name="dateOfBirth">The date of birth, where one is retained.</param>
    /// <returns>The profile.</returns>
    public static Profile Existing(
        SubjectId subject,
        DisplayName? displayName,
        LegalName? legalName,
        DateOnly? dateOfBirth) =>
        new(subject, displayName, legalName, dateOfBirth);

    /// <summary>
    /// Sets or clears the display name.
    /// </summary>
    /// <param name="name">The name, or nothing to clear it.</param>
    public void SetDisplayName(DisplayName? name) => DisplayName = name;

    /// <summary>
    /// Sets or clears the legal name. Whether the deployment collects one at all is
    /// <c>profile.legalname</c>, which the caller reads.
    /// </summary>
    /// <param name="name">The name, or nothing to clear it.</param>
    public void SetLegalName(LegalName? name) => LegalName = name;

    /// <summary>
    /// Records the date entered at the age step, or a correction to it. The date is
    /// immutable to the person (REG-PROF-001); which callers reach this is the
    /// endpoint's business, not the profile's.
    /// </summary>
    /// <param name="date">The date of birth.</param>
    public void RecordDateOfBirth(DateOnly date) => DateOfBirth = date;

    /// <summary>
    /// Forgets the date of birth, for a deployment that retains only the derived
    /// affirmation (REG-PROF-002).
    /// </summary>
    public void ForgetDateOfBirth() => DateOfBirth = null;
}
