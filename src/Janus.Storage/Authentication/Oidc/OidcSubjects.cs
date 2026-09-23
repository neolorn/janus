using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// What the protocol calls a subject, read as the account it names.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-004 and PRIV-ERASE-001. Every row the provider writes hangs
/// from an account of this deployment, so a subject that is not one of them is not
/// stored under a name that resembles one: it is refused where it arrives.
/// </remarks>
internal static class OidcSubjects
{
    /// <summary>
    /// The account a subject names.
    /// </summary>
    /// <param name="subject">What the protocol carried.</param>
    /// <returns>The account.</returns>
    /// <exception cref="ArgumentException">It names no account of this deployment.</exception>
    public static SubjectId Of(string? subject) =>
        Find(subject) ?? throw new ArgumentException(
            "The subject names no account of this deployment.",
            nameof(subject));

    /// <summary>
    /// The account a subject names, where it names one.
    /// </summary>
    /// <param name="subject">What the protocol carried.</param>
    /// <returns>The account, or nothing.</returns>
    public static SubjectId? Find(string? subject) =>
        Guid.TryParse(subject, out Guid held) ? new SubjectId(held) : null;
}
