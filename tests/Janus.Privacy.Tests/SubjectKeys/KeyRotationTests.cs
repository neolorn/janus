using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Xunit;

namespace Janus.Privacy.Tests.SubjectKeys;

/// <summary>
/// How the rotation of the key-encryption key leaves its unit of work where it is
/// refused: before it begins where the refusal reads nothing, and rolled back where it
/// is decided with the progress held.
/// </summary>
[Trait("kind", "unit")]
public sealed class KeyRotationTests : IAsyncDisposable
{
    private const KeyRotationKind Kind = KeyRotationKind.KeyEncryptionKey;

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly KeyRotationStoreInMemory _store = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly KeyRingInMemory _ring = new();
    private readonly FixedClock _clock = new(Noon);

    private KeyRotation Rotation => new(_store, _work, _audit, _clock, _ring);

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
        Result<KeyRotationProgress> refused = await Rotation.ReWrapAsync(TestContext.Current.CancellationToken);

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
        _ring.KeyEncryptionKeys = Keys(current: 2, 1, 2);
        _store.Latest = KeyRotationProgress.Started(Kind, 1, Noon);

        Result<KeyRotationProgress> refused = await Rotation.ReWrapAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, Code(refused));
        Assert.Empty(_audit.Entries);
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a retirement that finds the rotation retired by another run
    /// while it waited for the progress is refused under that hold, and rolls its unit
    /// of work back; the read and the sweep before it wrote nothing and rolled back too
    /// (AC10).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ARetirementRefusedWithItsProgressHeldRollsBackAsync()
    {
        _ring.KeyEncryptionKeys = Keys(current: 1, 1);
        _store.Latest = Completed(retiredAt: null);

        int held = 0;

        _store.Holding = () =>
        {
            if (++held == 2)
            {
                _store.Latest = Completed(retiredAt: Noon);
            }
        };

        Result<KeyRetirement> refused = await Rotation.RetireAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RotationNotReady, Code(refused));
        Assert.Empty(_audit.Entries);
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(3, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10: a pass and a sweep that find nothing left write nothing and
    /// roll their units of work back; the start and the completion, which write,
    /// commit.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_APassWithNothingLeftIsRolledBackAsync()
    {
        _ring.KeyEncryptionKeys = Keys(current: 1, 1);

        Result<KeyRotationProgress> rotated = await Rotation.ReWrapAsync(TestContext.Current.CancellationToken);

        Assert.Null(Code(rotated));
        Assert.NotNull(_store.Latest!.CompletedAt);
        Assert.False(_work.Open);
        Assert.Equal(2, _work.Committed);
        Assert.Equal(2, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10: a rotation another run completed while this one waited for
    /// the progress answers the rotation as that run left it, having written nothing at
    /// its completion, and rolls that unit of work back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ACompletionAnotherRunMadeIsRolledBackAsync()
    {
        _ring.KeyEncryptionKeys = Keys(current: 1, 1);

        int held = 0;

        _store.Holding = () =>
        {
            if (++held == 4)
            {
                _store.Latest = Completed(retiredAt: null);
            }
        };

        Result<KeyRotationProgress> rotated = await Rotation.ReWrapAsync(TestContext.Current.CancellationToken);

        Assert.Null(Code(rotated));
        Assert.Single(_audit.Entries);
        Assert.False(_work.Open);
        Assert.Equal(1, _work.Committed);
        Assert.Equal(3, _work.RolledBack);
    }

    private static KeyRotationProgress Completed(DateTimeOffset? retiredAt) =>
        KeyRotationProgress.Existing(Kind, 1, lastKey: null, processed: 0, Noon, completedAt: Noon, retiredAt);

    private static KeyEncryptionKeys Keys(int current, params int[] versions)
    {
        var held = new Dictionary<int, ReadOnlyMemory<byte>>();

        foreach (int version in versions)
        {
            held[version] = new byte[32];
        }

        return new KeyEncryptionKeys(current, held);
    }

    private static ErrorCode? Code<TValue>(Result<TValue> outcome) =>
        outcome.Match(_ => default(ErrorCode?), error => error.Code);
}
