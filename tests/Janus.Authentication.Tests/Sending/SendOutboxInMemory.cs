using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The messages admitted and not yet carried, so a test can read what was written
/// before a handler was asked, what was left behind when one refused, and which
/// attempt holds which row.
/// </summary>
internal sealed class SendOutboxInMemory : ISendOutbox
{
    private readonly Dictionary<SendDeliveryId, SendDelivery> _held = [];
    private readonly Dictionary<SendDeliveryId, DateTimeOffset> _claims = [];
    private readonly HashSet<SendDeliveryId> _erased = [];

    /// <summary>
    /// The unit of work the operations under test run in. Where a test names it, what
    /// the outbox wrote inside a unit of work that rolled back is put back.
    /// </summary>
    public UnitOfWorkInMemory? Work { get; set; }

    /// <summary>
    /// What is still waiting, oldest first.
    /// </summary>
    public IReadOnlyList<SendDelivery> Waiting =>
        [.. _held.Values.OrderBy(delivery => delivery.RecordedAt).ThenBy(delivery => delivery.Id.Value)];

    /// <summary>
    /// Every message ever written, in order, whether or not it is still waiting or its
    /// unit of work committed.
    /// </summary>
    public List<SendDelivery> Written { get; } = [];

    /// <summary>
    /// Every claim an attempt took, in order.
    /// </summary>
    public List<SendClaim> Claimed { get; } = [];

    /// <summary>
    /// What a test does once a claim has been taken and before it is answered, standing
    /// in for another attempt that reaches the row meanwhile.
    /// </summary>
    public Action<SendClaim>? Claiming { get; set; }

    /// <summary>
    /// Stands in for another attempt that takes the row over, as one would once the
    /// claim on it had timed out.
    /// </summary>
    /// <param name="delivery">The row.</param>
    /// <param name="until">When the other attempt's claim times out.</param>
    public void TakeOver(SendDeliveryId delivery, DateTimeOffset until) => _claims[delivery] = until;

    /// <summary>
    /// Stands in for an attempt that released the row and rescheduled it.
    /// </summary>
    /// <param name="delivery">The row.</param>
    /// <param name="next">When its next attempt is due.</param>
    public void Reschedule(SendDeliveryId delivery, DateTimeOffset next)
    {
        _held[delivery] = _held[delivery] with { NextAttemptAt = next };
        _ = _claims.Remove(delivery);
    }

    /// <summary>
    /// Stands in for an erasure that overwrote the key of one row, which then reads as
    /// nothing but the hash of its reference.
    /// </summary>
    /// <param name="delivery">The row.</param>
    public void Erase(SendDeliveryId delivery) => _ = _erased.Add(delivery);

    /// <inheritdoc/>
    public ValueTask AddAsync(SendDelivery delivery, CancellationToken cancellationToken)
    {
        Undoing();

        _held[delivery!.Id] = delivery;
        Written.Add(delivery);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SendDeliveryId>> DueAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<SendDeliveryId>>(
        [
            .. Waiting
                .Where(delivery => delivery.NextAttemptAt <= now
                    && (!_claims.TryGetValue(delivery.Id, out DateTimeOffset until) || until <= now))
                .Select(delivery => delivery.Id)
                .Take(count),
        ]);

    /// <inheritdoc/>
    public ValueTask<SendClaim?> ClaimAsync(
        SendDeliveryId delivery,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!_held.TryGetValue(delivery, out SendDelivery? held)
            || held.NextAttemptAt > now
            || (_claims.TryGetValue(delivery, out DateTimeOffset until) && until > now))
        {
            return ValueTask.FromResult<SendClaim?>(null);
        }

        Undoing();

        var claim = new SendClaim(delivery, now + timeout);

        _claims[delivery] = claim.Until;
        Claimed.Add(claim);
        Claiming?.Invoke(claim);

        return ValueTask.FromResult<SendClaim?>(claim);
    }

    /// <inheritdoc/>
    public ValueTask<SendDelivery?> FindAsync(
        SendDeliveryId delivery,
        CancellationToken cancellationToken) =>
        _erased.Contains(delivery)
            ? throw new CryptographicException("The key has been erased.")
            : ValueTask.FromResult(_held.TryGetValue(delivery, out SendDelivery? held) ? held : null);

    /// <inheritdoc/>
    public ValueTask<bool> WaitsAsync(SendDeliveryId delivery, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_held.ContainsKey(delivery));

    /// <inheritdoc/>
    public ValueTask<byte[]?> ErasedAsync(SendDeliveryId delivery, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _erased.Contains(delivery) && _held.TryGetValue(delivery, out SendDelivery? held)
                ? SendReferences.Of(held.Reference)
                : null);

    /// <inheritdoc/>
    public ValueTask<bool> RemoveAsync(SendClaim claim, CancellationToken cancellationToken)
    {
        if (!Holds(claim))
        {
            return ValueTask.FromResult(false);
        }

        Undoing();

        _ = _held.Remove(claim.Delivery);
        _ = _claims.Remove(claim.Delivery);

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> RecordAsync(SendDelivery delivery, SendClaim claim, CancellationToken cancellationToken)
    {
        if (!Holds(claim))
        {
            return ValueTask.FromResult(false);
        }

        Undoing();

        _held[claim.Delivery] = delivery;
        _ = _claims.Remove(claim.Delivery);

        return ValueTask.FromResult(true);
    }

    private bool Holds(SendClaim claim) =>
        _held.ContainsKey(claim.Delivery)
        && _claims.TryGetValue(claim.Delivery, out DateTimeOffset until)
        && until == claim.Until;

    // What the outbox holds now is what a rollback of the unit of work in progress puts
    // back.
    private void Undoing()
    {
        if (Work is not { Open: true })
        {
            return;
        }

        var held = new Dictionary<SendDeliveryId, SendDelivery>(_held);
        var claims = new Dictionary<SendDeliveryId, DateTimeOffset>(_claims);

        Work.Undoing(() =>
        {
            _held.Clear();
            _claims.Clear();

            foreach (KeyValuePair<SendDeliveryId, SendDelivery> one in held)
            {
                _held[one.Key] = one.Value;
            }

            foreach (KeyValuePair<SendDeliveryId, DateTimeOffset> one in claims)
            {
                _claims[one.Key] = one.Value;
            }
        });
    }
}
