using System;
using Janus.Core;

namespace Janus.Identity.Organizations;

/// <summary>
/// A domain entity within the one identity pool: the thing authentication policy
/// attaches to and the thing a membership names. It is no tenancy and no isolation
/// boundary.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-001, IDN-ORG-002, IDN-ORG-003, IDN-ORG-005 and IDN-PRIN-003.
/// Deletion is a window, not a removal: the request suspends the organization at once,
/// the window runs for <c>organization.deletion.grace</c>, a cancellation inside it
/// restores everything, and its end leaves the row where it was with the identifying
/// data of its members unreadable.
/// </remarks>
internal sealed class Organization
{
    private Organization(OrganizationId id, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// The identifier a membership, a grant and an audit record reference.
    /// </summary>
    public OrganizationId Id { get; }

    /// <summary>
    /// What the organization is called. It is one of the two plaintext columns a
    /// person spells, so it is compared and sorted under the case-insensitive
    /// collation.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// When the organization was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When deletion was requested, where a window is running or has run. Access stops
    /// at this instant.
    /// </summary>
    public DateTimeOffset? DeletionRequestedAt { get; private set; }

    /// <summary>
    /// When the erasure at the end of the window executed.
    /// </summary>
    public DateTimeOffset? ErasedAt { get; private set; }

    /// <summary>
    /// Whether the organization is suspended: its deletion has been requested and the
    /// request has not been cancelled. Suspension is the deletion request's own effect
    /// and is no state an administrator sets on its own.
    /// </summary>
    public bool IsSuspended => DeletionRequestedAt is not null;

    /// <summary>
    /// A new organization.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="name">What it is called.</param>
    /// <param name="createdAt">When it was created.</param>
    /// <returns>The organization.</returns>
    /// <exception cref="ArgumentException">The name is absent or blank.</exception>
    public static Organization Create(OrganizationId id, string name, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Organization(id, name, createdAt);
    }

    /// <summary>
    /// The organization as it already stands. This is the store's translation of a
    /// stored row and no change an administrator made.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="name">What it is called.</param>
    /// <param name="createdAt">When it was created.</param>
    /// <param name="deletionRequestedAt">When deletion was requested, where it was.</param>
    /// <param name="erasedAt">When the erasure executed, where it did.</param>
    /// <returns>The organization.</returns>
    /// <exception cref="ArgumentException">The name is absent or blank.</exception>
    public static Organization Existing(
        OrganizationId id,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset? deletionRequestedAt,
        DateTimeOffset? erasedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Organization(id, name, createdAt)
        {
            DeletionRequestedAt = deletionRequestedAt,
            ErasedAt = erasedAt,
        };
    }

    /// <summary>
    /// Requests the organization's deletion, which suspends it at once and starts the
    /// grace window.
    /// </summary>
    /// <param name="at">The instant of the request.</param>
    /// <exception cref="InvalidOperationException">
    /// A window is already running, or the erasure has already executed.
    /// </exception>
    public void RequestDeletion(DateTimeOffset at)
    {
        if (ErasedAt is not null)
        {
            throw new InvalidOperationException("An erased organization is not deleted again.");
        }

        if (DeletionRequestedAt is not null)
        {
            throw new InvalidOperationException("The organization is already being deleted.");
        }

        DeletionRequestedAt = at;
    }

    /// <summary>
    /// Cancels the deletion, which lifts the suspension and restores every membership
    /// and grant the request left standing.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No window is running, or the erasure has already executed.
    /// </exception>
    public void CancelDeletion()
    {
        if (ErasedAt is not null)
        {
            throw new InvalidOperationException("An erasure that has executed is not cancelled.");
        }

        if (DeletionRequestedAt is null)
        {
            throw new InvalidOperationException("The organization is not being deleted.");
        }

        DeletionRequestedAt = null;
    }

    /// <summary>
    /// Records the erasure at the end of the grace window. The row stays where it is
    /// and the identifier goes on resolving.
    /// </summary>
    /// <param name="at">The instant the erasure executed.</param>
    /// <param name="window">What <c>organization.deletion.grace</c> allows.</param>
    /// <exception cref="InvalidOperationException">
    /// No window is running, the window has not elapsed, or the erasure has already
    /// executed.
    /// </exception>
    public void RecordErasure(DateTimeOffset at, TimeSpan window)
    {
        if (ErasedAt is not null)
        {
            throw new InvalidOperationException("The erasure has already executed.");
        }

        if (DeletionRequestedAt is not { } requestedAt)
        {
            throw new InvalidOperationException("The organization is not being deleted.");
        }

        if (at < requestedAt + window)
        {
            throw new InvalidOperationException("The grace window has not elapsed.");
        }

        ErasedAt = at;
    }
}
