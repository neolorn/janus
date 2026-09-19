using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// The browsers an account knows, over the <c>devices</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements AUTH-FACT-015, AUTH-FACT-016 and CONV-DESIGN-003.</remarks>
internal sealed class DeviceStore(JanusDbContext context) : IDeviceStore
{
    /// <inheritdoc/>
    public async ValueTask<Device?> FindByFingerprintAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        DeviceRecord? record = await context.Devices
            .FirstOrDefaultAsync(device => device.TokenFingerprint == fingerprint, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    public async ValueTask<Device?> FindAsync(DeviceId id, CancellationToken cancellationToken)
    {
        DeviceRecord? record = await context.Devices
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Device>> StandingOfAsync(
        SubjectId subject,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        [
            .. (await context.Devices
                .Where(device => device.Subject == subject
                    && !device.Revoked
                    && device.ExpiresAt > now)
                .OrderBy(device => device.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(Read),
        ];

    /// <inheritdoc/>
    public ValueTask AddAsync(Device device, byte[] fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(fingerprint);

        context.Devices.Add(new DeviceRecord
        {
            Id = device.Id,
            Subject = device.Subject,
            Kind = device.Kind,
            Label = device.Label.Value,
            TokenFingerprint = fingerprint,
            CreatedAt = device.CreatedAt,
            LastUsedAt = device.LastUsedAt,
            ExpiresAt = device.ExpiresAt,
            ConsecutiveFailures = device.ConsecutiveFailures,
            Revoked = device.Revoked,
        });

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Device device, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);

        DeviceRecord record = await context.Devices
            .FindAsync([device.Id], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The browser has no row to carry the change.");

        record.LastUsedAt = device.LastUsedAt;
        record.ConsecutiveFailures = device.ConsecutiveFailures;
        record.Revoked = device.Revoked;
    }

    private static Device Read(DeviceRecord record) => Device.Existing(
        record.Id,
        record.Subject,
        record.Kind,
        Label(record.Label),
        record.CreatedAt,
        record.LastUsedAt,
        record.ExpiresAt,
        record.ConsecutiveFailures,
        record.Revoked);

    // A label this library wrote is a label this library accepts, so a stored value
    // that no longer parses is a corrupted row and not a label to drop quietly.
    private static CredentialLabel Label(string stored) =>
        CredentialLabel.TryParse(stored, out CredentialLabel label)
            ? label
            : throw new InvalidOperationException("The stored label is not a label.");
}
