using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Xunit;

namespace Janus.Privacy.Tests.SubjectKeys;

/// <summary>
/// How the rotation of the fingerprint key leaves its unit of work where it is refused:
/// before it begins where the refusal reads nothing, and rolled back where it is
/// decided with the progress held.
/// </summary>
[Trait("kind", "unit")]
public sealed class FingerprintKeyRotationTests : IAsyncDisposable
{
    private const KeyRotationKind Kind = KeyRotationKind.FingerprintKey;

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly KeyRotationStoreInMemory _rotations = new();
    private readonly FingerprintRotationStoreInMemory _store = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly KeyRingInMemory _ring = new();
    private readonly FixedClock _clock = new(Noon);

    private FingerprintKeyRotation Rotation => new(_rotations, _store, _work, _audit, _clock, _ring);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// CONV-DESIGN-003: a rotation the command was handed no key for reads nothing that
    /// needs the transaction, so it is refused before the unit of work begins.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_ARotationWithoutItsKeysBeginsNoUnitOfWorkAsync()
    {
        Result<KeyRotationProgress> refused = await Rotation.RecomputeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, Code(refused));
        Assert.Equal(0, _work.Opened);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a rotation to a version other than the one that stopped is
    /// refused with the progress held, and rolls its unit of work back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ARotationRefusedWithItsProgressHeldRollsBackAsync()
    {
        _ring.FingerprintKeys = Keys(current: 2, 1, 2);
        _rotations.Latest = KeyRotationProgress.Started(Kind, 1, Noon);

        Result<KeyRotationProgress> refused = await Rotation.RecomputeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, Code(refused));
        Assert.Empty(_audit.Entries);
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a retirement that finds the rotation retired by another run
    /// while it waited for the progress is refused under that hold, and rolls its unit
    /// of work back; the read and the sweep before it committed on their own.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ARetirementRefusedWithItsProgressHeldRollsBackAsync()
    {
        _ring.FingerprintKeys = Keys(current: 1, 1);
        _rotations.Latest = Completed(retiredAt: null);

        int held = 0;

        _rotations.Holding = () =>
        {
            if (++held == 2)
            {
                _rotations.Latest = Completed(retiredAt: Noon);
            }
        };

        Result<KeyRetirement> refused = await Rotation.RetireAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RotationNotReady, Code(refused));
        Assert.Empty(_audit.Entries);
        Assert.False(_work.Open);
        Assert.Equal(2, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, OPS-SEC-003 AC4: a retirement refused because fingerprints
    /// still stand under a previous version forgets nothing and rolls its unit of work
    /// back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ARetirementRefusedForWhatStillStandsRollsBackAsync()
    {
        _ring.FingerprintKeys = Keys(current: 1, 1);
        _rotations.Latest = Completed(retiredAt: null);
        _store.Standing = 3;

        Result<KeyRetirement> refused = await Rotation.RetireAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RotationNotReady, Code(refused));
        Assert.Equal(
            3,
            refused.Match(_ => 0, error => error.Details["pending"].GetInt32()));
        Assert.Equal(0, _store.Forgotten);
        Assert.Null(_rotations.Latest!.RetiredAt);
        Assert.False(_work.Open);
        Assert.Equal(2, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    private static KeyRotationProgress Completed(DateTimeOffset? retiredAt) =>
        KeyRotationProgress.Existing(Kind, 1, lastKey: null, processed: 0, Noon, completedAt: Noon, retiredAt);

    private static FingerprintKeys Keys(int current, params int[] versions)
    {
        var held = new Dictionary<int, ReadOnlyMemory<byte>>();

        foreach (int version in versions)
        {
            held[version] = new byte[FingerprintKeys.MinimumLength];
        }

        return new FingerprintKeys(current, held);
    }

    private static ErrorCode? Code<TValue>(Result<TValue> outcome) =>
        outcome.Match(_ => default(ErrorCode?), error => error.Code);
}
