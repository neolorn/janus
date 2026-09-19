using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// The browsers an account knows, held in memory as the table holds them: the row by
/// its identifier and the token by its fingerprint.
/// </summary>
internal sealed class DeviceStoreInMemory : IDeviceStore
{
    private readonly Dictionary<DeviceId, Device> _devices = [];
    private readonly Dictionary<string, DeviceId> _tokens = [];

    /// <summary>
    /// Every browser the store holds, revoked ones among them.
    /// </summary>
    public IReadOnlyCollection<Device> All => _devices.Values;

    /// <inheritdoc/>
    public ValueTask<Device?> FindByFingerprintAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _tokens.TryGetValue(Key(fingerprint), out DeviceId id) ? _devices[id] : null);

    /// <inheritdoc/>
    public ValueTask<Device?> FindAsync(DeviceId id, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_devices.GetValueOrDefault(id));

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Device>> StandingOfAsync(
        SubjectId subject,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Device>>(
            [.. _devices.Values.Where(device => device.Subject == subject && device.Stands(now))]);

    /// <inheritdoc/>
    public ValueTask AddAsync(Device device, byte[] fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);

        _devices[device.Id] = device;
        _tokens[Key(fingerprint)] = device.Id;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(Device device, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);

        _devices[device.Id] = device;

        return ValueTask.CompletedTask;
    }

    private static string Key(byte[] fingerprint) => Convert.ToHexString(fingerprint);
}
