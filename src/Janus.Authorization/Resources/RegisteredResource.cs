using Janus.Core;

namespace Janus.Authorization.Resources;

/// <summary>
/// One of the host's records as the library knows it: which kind of thing it is,
/// which organization owns it, whose data it is, and what contains it. The library
/// never reads the host's table (LIB-HOST-002), so the host says this when it creates
/// or moves the record.
/// </summary>
/// <remarks>
/// Implements AUTHZ-INHERIT-001, AUTHZ-INHERIT-002, AUTHZ-SCOPE-001 and LIB-HOST-002.
/// The ancestry that inheritance is resolved through is written from this, in the same
/// transaction, so permission data and business data cannot diverge.
/// </remarks>
internal sealed class RegisteredResource
{
    private RegisteredResource(
        ResourceReference reference,
        OrganizationId organization,
        SubjectId? subject,
        ResourceReference? containedIn)
    {
        Reference = reference;
        Organization = organization;
        Subject = subject;
        ContainedIn = containedIn;
    }

    /// <summary>
    /// Which record it is.
    /// </summary>
    public ResourceReference Reference { get; }

    /// <summary>
    /// The organization owning it, which is what every evaluation on it is scoped to.
    /// </summary>
    public OrganizationId Organization { get; }

    /// <summary>
    /// The data subject of the record, as the column the type declares for its
    /// encrypted fields holds it, or nothing where the record is about nobody
    /// (PRIV-RIGHT-005a, PRIV-SENS-002).
    /// </summary>
    public SubjectId? Subject { get; }

    /// <summary>
    /// What contains it, or nothing where it is contained in nothing.
    /// </summary>
    public ResourceReference? ContainedIn { get; private set; }

    /// <summary>
    /// A record the host has just created.
    /// </summary>
    /// <param name="reference">Which record.</param>
    /// <param name="organization">The organization owning it.</param>
    /// <param name="subject">Whose data it is, where it is anybody's.</param>
    /// <param name="containedIn">What contains it, where anything does.</param>
    /// <returns>The registration.</returns>
    public static RegisteredResource Create(
        ResourceReference reference,
        OrganizationId organization,
        SubjectId? subject,
        ResourceReference? containedIn) =>
        new(reference, organization, subject, containedIn);

    /// <summary>
    /// A record as its row holds it.
    /// </summary>
    /// <param name="reference">Which record.</param>
    /// <param name="organization">The organization owning it.</param>
    /// <param name="subject">Whose data it is, where it is anybody's.</param>
    /// <param name="containedIn">What contains it.</param>
    /// <returns>The registration.</returns>
    public static RegisteredResource Existing(
        ResourceReference reference,
        OrganizationId organization,
        SubjectId? subject,
        ResourceReference? containedIn) =>
        new(reference, organization, subject, containedIn);

    /// <summary>
    /// Moves the record under another container, or out of one altogether.
    /// </summary>
    /// <param name="containedIn">The new container, or nothing.</param>
    public void MoveTo(ResourceReference? containedIn) => ContainedIn = containedIn;
}
