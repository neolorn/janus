using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Passwords;

/// <summary>
/// An account's password, over the <c>passwords</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements AUTH-PASS-007 and CONV-DESIGN-003.</remarks>
internal sealed class PasswordStore(StoreContext context) : IPasswordStore
{
    /// <inheritdoc/>
    public async ValueTask<Password?> FindAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        PasswordRecord? record = await RowAsync(subject, cancellationToken).ConfigureAwait(false);

        return record is null ? null : Password.Existing(
            record.Subject,
            PasswordHash.Parse(record.Hash),
            record.MeetsSingleFactorFloor,
            record.SetAt,
            record.ChangeRequired);
    }

    /// <inheritdoc/>
    public async ValueTask SetAsync(Password password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);

        PasswordRecord? record = await RowAsync(password.Subject, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            record = new PasswordRecord { Subject = password.Subject };
            context.Passwords.Add(record);
        }

        record.Hash = password.Hash.Encoded;
        record.MeetsSingleFactorFloor = password.MeetsSingleFactorFloor;
        record.SetAt = password.SetAt;
        record.ChangeRequired = password.ChangeRequired;
    }

    /// <inheritdoc/>
    public async ValueTask RehashAsync(Password password, PasswordHash read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(read);

        // D-166 X3: one update conditional on the hash the password verified against, so
        // a sign-in with the old password never writes it back over a new one.
        string written = password.Hash.Encoded;
        string verified = read.Encoded;

        _ = await context.Passwords
            .Where(record => record.Subject == password.Subject && record.Hash == verified)
            .ExecuteUpdateAsync(columns => columns.SetProperty(record => record.Hash, written), cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<PasswordRecord?> RowAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.Passwords.FindAsync([subject], cancellationToken).ConfigureAwait(false);
}
