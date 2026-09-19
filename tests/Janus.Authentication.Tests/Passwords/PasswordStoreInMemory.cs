using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Tests.Passwords;

/// <summary>
/// The one password an account holds, kept in memory as the table keeps it, and a
/// count of the rehashes carried onto it.
/// </summary>
internal sealed class PasswordStoreInMemory : IPasswordStore
{
    private readonly Dictionary<SubjectId, Password> _passwords = [];

    /// <summary>
    /// How many times a hash was carried onto raised parameters.
    /// </summary>
    public int Rehashed { get; private set; }

    /// <summary>
    /// Puts a password on an account, so that a test about something a password is a
    /// precondition of does not have to hash one.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="at">When it was set.</param>
    public void Hold(SubjectId subject, DateTimeOffset at) =>
        _passwords[subject] = Password.Existing(
            subject,
            PasswordHash.Of(new Argon2StrengthClass(19456, 2), 1, new byte[16], new byte[32]),
            meetsSingleFactorFloor: true,
            at);

    /// <inheritdoc/>
    public ValueTask<Password?> FindAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_passwords.GetValueOrDefault(subject));

    /// <inheritdoc/>
    public ValueTask SetAsync(Password password, CancellationToken cancellationToken)
    {
        _passwords[password.Subject] = password;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RehashAsync(Password password, CancellationToken cancellationToken)
    {
        _passwords[password.Subject] = password;
        Rehashed++;

        return ValueTask.CompletedTask;
    }
}
