using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Sessions;

/// <summary>
/// Sessions, over the <c>sessions</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="randomness">The randomness the initialisation vector is drawn from.</param>
/// <remarks>
/// Implements AUTH-SESS-001, AUTH-SESS-003, AUTH-SESS-013 and CONV-DESIGN-003. Where a
/// session was used from is held under the person's key, so a dump yields neither the
/// address nor the city, and erasure leaves both unreadable.
/// </remarks>
internal sealed class SessionStore(
    JanusDbContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : ISessionStore
{
    /// <inheritdoc/>
    public async ValueTask<Session?> FindAsync(SessionId id, CancellationToken cancellationToken)
    {
        SessionRecord? record = await context.Sessions
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ReadAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Session?> FindByFingerprintAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        SessionRecord? record = await context.Sessions
            .FirstOrDefaultAsync(session => session.SecretFingerprint == fingerprint, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ReadAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(
        Session session,
        byte[] fingerprint,
        byte[] csrfFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(csrfFingerprint);

        byte[] dataKey = await DataKeyAsync(session.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            context.Sessions.Add(new SessionRecord
            {
                Id = session.Id,
                Spine = session.Spine,
                Type = session.Type,
                Subject = session.Subject,
                SecretFingerprint = fingerprint,
                CsrfFingerprint = csrfFingerprint,
                CreatedAt = session.CreatedAt,
                LastSeenAt = session.LastSeenAt,
                Attained = session.Attained,
                AttainedAt = session.AttainedAt,
                PhishingResistant = session.PhishingResistant,
                PhishingResistantAt = session.PhishingResistantAt,
                OriginBrowser = session.Origin.Device.Browser,
                OriginOs = session.Origin.Device.Os,
                OriginPlace = Written(
                    dataKey,
                    session.Subject,
                    SessionConfiguration.OriginPlaceColumn,
                    session.Origin),
                LastSeenBrowser = session.LastSeen.Device.Browser,
                LastSeenOs = session.LastSeen.Device.Os,
                LastSeenPlace = Written(
                    dataKey,
                    session.Subject,
                    SessionConfiguration.LastSeenPlaceColumn,
                    session.LastSeen),
                IdleExpiry = session.IdleExpiry,
                AbsoluteExpiry = session.AbsoluteExpiry,
                EndedAt = session.EndedAt,
                SatisfiesEveryGate = session.SatisfiesEveryGate,
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        SessionRecord record = await context.Sessions
            .FindAsync([session.Id], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The session has no row to carry the change.");

        byte[] dataKey = await DataKeyAsync(session.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            record.LastSeenAt = session.LastSeenAt;
            record.Attained = session.Attained;
            record.AttainedAt = session.AttainedAt;
            record.PhishingResistant = session.PhishingResistant;
            record.PhishingResistantAt = session.PhishingResistantAt;
            record.LastSeenBrowser = session.LastSeen.Device.Browser;
            record.LastSeenOs = session.LastSeen.Device.Os;
            record.LastSeenPlace = Written(
                dataKey,
                session.Subject,
                SessionConfiguration.LastSeenPlaceColumn,
                session.LastSeen);
            record.IdleExpiry = session.IdleExpiry;
            record.EndedAt = session.EndedAt;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask ReplaceSecretAsync(
        SessionId id,
        byte[] fingerprint,
        byte[] csrfFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(csrfFingerprint);

        SessionRecord record = await context.Sessions
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The session has no row to carry the new secret.");

        record.SecretFingerprint = fingerprint;
        record.CsrfFingerprint = csrfFingerprint;
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]?> CsrfFingerprintAsync(
        SessionId id,
        CancellationToken cancellationToken)
    {
        SessionRecord? record = await context.Sessions
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        return record?.CsrfFingerprint;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Session>> LiveOfAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        List<SessionRecord> records = await context.Sessions
            .Where(session => session.Subject == subject
                && session.EndedAt == null
                && session.IdleExpiry > at
                && session.AbsoluteExpiry > at)
            .OrderBy(session => session.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (records.Count is 0)
        {
            return [];
        }

        byte[] dataKey = await DataKeyAsync(subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return [.. records.Select(record => Read(dataKey, record))];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask EndSpineAsync(
        SessionId spine,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await context.Sessions
            .Where(session => session.Spine == spine && session.EndedAt == null)
            .ExecuteUpdateAsync(
                session => session.SetProperty(row => row.EndedAt, at),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask EndAccountAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await context.Sessions
            .Where(session => session.Subject == subject && session.EndedAt == null)
            .ExecuteUpdateAsync(
                session => session.SetProperty(row => row.EndedAt, at),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask EndEveryAsync(DateTimeOffset at, CancellationToken cancellationToken) =>
        await context.Sessions
            .Where(session => session.EndedAt == null)
            .ExecuteUpdateAsync(
                session => session.SetProperty(row => row.EndedAt, at),
                cancellationToken)
            .ConfigureAwait(false);

    private byte[] Written(
        ReadOnlySpan<byte> dataKey,
        SubjectId subject,
        string column,
        SessionOrigin origin) =>
        PersonalFieldCipher.Encrypt(
            dataKey,
            new PersonalFieldLocation(subject, SessionConfiguration.Table, column),
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new SessionPlace(origin.Address, origin.Location?.City, origin.Location?.Country),
                SessionPlaceJson.Default.SessionPlace)),
            randomness);

    private static Session Read(ReadOnlySpan<byte> dataKey, SessionRecord record) => Session.Existing(
        record.Id,
        record.Spine,
        record.Type,
        record.Subject,
        record.CreatedAt,
        record.LastSeenAt,
        record.Attained,
        record.AttainedAt,
        record.PhishingResistant,
        record.PhishingResistantAt,
        Origin(
            dataKey,
            record,
            SessionConfiguration.OriginPlaceColumn,
            record.OriginBrowser,
            record.OriginOs,
            record.OriginPlace),
        Origin(
            dataKey,
            record,
            SessionConfiguration.LastSeenPlaceColumn,
            record.LastSeenBrowser,
            record.LastSeenOs,
            record.LastSeenPlace),
        record.IdleExpiry,
        record.AbsoluteExpiry,
        record.EndedAt,
        record.SatisfiesEveryGate);

    private static SessionOrigin Origin(
        ReadOnlySpan<byte> dataKey,
        SessionRecord record,
        string column,
        string browser,
        string os,
        byte[] stored)
    {
        SessionPlace place = JsonSerializer.Deserialize(
            Encoding.UTF8.GetString(PersonalFieldCipher.Decrypt(
                dataKey,
                new PersonalFieldLocation(record.Subject, SessionConfiguration.Table, column),
                stored)),
            SessionPlaceJson.Default.SessionPlace)
            ?? throw new InvalidOperationException("The stored place holds no address.");

        return new SessionOrigin(
            place.Address,
            new DeviceDescription(browser, os),
            place.City is null && place.Country is null
                ? null
                : new SessionLocation(place.City, place.Country));
    }

    private async ValueTask<Session> ReadAsync(
        SessionRecord record,
        CancellationToken cancellationToken)
    {
        byte[] dataKey = await DataKeyAsync(record.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return Read(dataKey, record);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to read its sessions under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }
}
