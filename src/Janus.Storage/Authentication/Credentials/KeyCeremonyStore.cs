using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Credentials;

/// <summary>
/// The creation ceremonies accounts have open, over the <c>key_ceremonies</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements AUTH-FACT-014 and CONV-DESIGN-003.</remarks>
internal sealed class KeyCeremonyStore(StoreContext context) : IKeyCeremonyStore
{
    /// <inheritdoc/>
    public async ValueTask<KeyCeremony?> FindAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        KeyCeremonyRecord? record = await context.KeyCeremonies
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false);

        return record is null
            ? null
            : KeyCeremony.Existing(
                record.Subject,
                record.Kind,
                record.Challenge,
                record.Upgrading,
                record.IssuedAt,
                record.ExpiresAt);
    }

    /// <inheritdoc/>
    public async ValueTask ReplaceAsync(KeyCeremony ceremony, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ceremony);

        KeyCeremonyRecord? held = await context.KeyCeremonies
            .FindAsync([ceremony.Subject], cancellationToken)
            .ConfigureAwait(false);

        if (held is null)
        {
            await context.KeyCeremonies
                .AddAsync(
                    new KeyCeremonyRecord
                    {
                        Subject = ceremony.Subject,
                        Kind = ceremony.Kind,
                        Challenge = ceremony.Challenge,
                        Upgrading = ceremony.Upgrading,
                        IssuedAt = ceremony.IssuedAt,
                        ExpiresAt = ceremony.ExpiresAt,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        held.Kind = ceremony.Kind;
        held.Challenge = ceremony.Challenge;
        held.Upgrading = ceremony.Upgrading;
        held.IssuedAt = ceremony.IssuedAt;
        held.ExpiresAt = ceremony.ExpiresAt;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        KeyCeremonyRecord? record = await context.KeyCeremonies
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.KeyCeremonies.Remove(record);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.KeyCeremonies
            .Where(ceremony => ceremony.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
