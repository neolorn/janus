using System;
using Janus.Authentication.Passwords;
using Janus.Core;

namespace Janus.Authentication.BreakGlass;

/// <summary>
/// One issue of the break-glass credential: its hash, who generated it and when, and
/// whether it was spent or replaced.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-002 and OPS-BOOT-004. The code itself is never held: the page
/// that shows it is the only place it exists, and what the system keeps is a hash of
/// it. At most one issue stands at a time; generating another replaces it.
/// </remarks>
internal sealed class BreakGlassCredential
{
    private BreakGlassCredential(
        BreakGlassCredentialId id,
        PasswordHash hash,
        SubjectId issuedBy,
        DateTimeOffset issuedAt,
        DateTimeOffset? consumedAt,
        DateTimeOffset? replacedAt)
    {
        Id = id;
        Hash = hash;
        IssuedBy = issuedBy;
        IssuedAt = issuedAt;
        ConsumedAt = consumedAt;
        ReplacedAt = replacedAt;
    }

    /// <summary>Which issue it is.</summary>
    public BreakGlassCredentialId Id { get; }

    /// <summary>The hash of the code, which is all that is kept of it.</summary>
    public PasswordHash Hash { get; }

    /// <summary>Who generated it: a system administrator or the emergency account.</summary>
    public SubjectId IssuedBy { get; }

    /// <summary>When it was generated.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>When it was used, and nothing where it has not been.</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>When a later issue replaced it, and nothing where none has.</summary>
    public DateTimeOffset? ReplacedAt { get; private set; }

    /// <summary>Whether it still opens a session: neither used nor replaced.</summary>
    public bool Stands => ConsumedAt is null && ReplacedAt is null;

    /// <summary>
    /// A newly generated issue.
    /// </summary>
    /// <param name="hash">The hash of the code.</param>
    /// <param name="issuedBy">Who generated it.</param>
    /// <param name="at">When.</param>
    /// <returns>The issue.</returns>
    /// <exception cref="ArgumentNullException">The hash is absent.</exception>
    public static BreakGlassCredential Issue(PasswordHash hash, SubjectId issuedBy, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(hash);

        return new BreakGlassCredential(BreakGlassCredentialId.Of(at), hash, issuedBy, at, consumedAt: null, replacedAt: null);
    }

    /// <summary>
    /// An issue as it already stands, which is the store's translation of its row and
    /// no change to it.
    /// </summary>
    /// <param name="id">Which issue it is.</param>
    /// <param name="hash">The hash of the code.</param>
    /// <param name="issuedBy">Who generated it.</param>
    /// <param name="issuedAt">When.</param>
    /// <param name="consumedAt">When it was used.</param>
    /// <param name="replacedAt">When it was replaced.</param>
    /// <returns>The issue.</returns>
    /// <exception cref="ArgumentNullException">The hash is absent.</exception>
    public static BreakGlassCredential Held(
        BreakGlassCredentialId id,
        PasswordHash hash,
        SubjectId issuedBy,
        DateTimeOffset issuedAt,
        DateTimeOffset? consumedAt,
        DateTimeOffset? replacedAt)
    {
        ArgumentNullException.ThrowIfNull(hash);

        return new BreakGlassCredential(id, hash, issuedBy, issuedAt, consumedAt, replacedAt);
    }

    /// <summary>
    /// Spends it, which is what its one use does.
    /// </summary>
    /// <param name="at">When.</param>
    /// <exception cref="InvalidOperationException">It no longer stands.</exception>
    public void Consume(DateTimeOffset at)
    {
        if (!Stands)
        {
            throw new InvalidOperationException("A spent or replaced credential opens nothing.");
        }

        ConsumedAt = at;
    }

    /// <summary>
    /// Replaces it, which generating another does.
    /// </summary>
    /// <param name="at">When.</param>
    /// <exception cref="InvalidOperationException">It no longer stands.</exception>
    public void Replace(DateTimeOffset at)
    {
        if (!Stands)
        {
            throw new InvalidOperationException("A spent or replaced credential is replaced by nothing.");
        }

        ReplacedAt = at;
    }
}
