using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The names no account takes as a username: the library's own, and whatever the host
/// adds to them.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-009 and LIB-HOST-001. A name is reserved in the form a
/// username is compared in, so a host that adds a name it spells differently reserves
/// the same name. Whether a free name is already taken, or held after an erasure, is
/// not this type's business.
/// </remarks>
public sealed class ReservedUsernames
{
    private static readonly string[] Always =
    [
        "admin",
        "administrator",
        "root",
        "support",
        "security",
        "postmaster",
        "abuse",
        "noreply",
        "emergency",
        "system",
        "help",
        "api",
        "www",
        "mail",
    ];

    private readonly HashSet<string> _reserved;

    private ReservedUsernames(HashSet<string> reserved) => _reserved = reserved;

    /// <summary>
    /// The list of a host that adds nothing to the library's.
    /// </summary>
    public static ReservedUsernames Default { get; } = Of([]);

    /// <summary>
    /// Every name reserved, in the form it is compared in.
    /// </summary>
    public IReadOnlyCollection<string> All => _reserved;

    /// <summary>
    /// Reads a host's additions to the library's list.
    /// </summary>
    /// <param name="added">The names the host adds.</param>
    /// <returns>The list.</returns>
    /// <exception cref="ArgumentNullException">The collection, or a name in it, is absent.</exception>
    public static ReservedUsernames Of(IEnumerable<string> added)
    {
        ArgumentNullException.ThrowIfNull(added);

        var reserved = new HashSet<string>(Always, StringComparer.OrdinalIgnoreCase);

        foreach (string name in added)
        {
            ArgumentNullException.ThrowIfNull(name);

            reserved.Add(Compared(name));
        }

        return new ReservedUsernames(reserved);
    }

    /// <summary>
    /// Whether a username is one nobody takes.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <returns>Whether it is reserved.</returns>
    public bool Holds(Username username) => _reserved.Contains(username.Value);

    // A name the host adds in a form no username could take is kept as it was given:
    // it matches nothing, which is what a name no username can equal should do.
    private static string Compared(string name) =>
        Username.TryParse(name, out Username username) ? username.Value : name.Trim();
}
