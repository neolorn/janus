using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The filling of the key ring and the recording of the mail server in use at the
/// start: what is held and chosen through it is what the ring lends and the mail server
/// in use answers (CONV-DESIGN-007, CONV-CODE-007, D-189).
/// </summary>
[Trait("kind", "unit")]
public sealed class KeyRingFillingTests
{
    private static readonly byte[] Wrapping = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

    private static readonly byte[] Fingerprint = [.. Enumerable.Range(33, 32).Select(value => (byte)value)];

    private static readonly byte[] Maintenance = "the-maintenance-credential"u8.ToArray();

    private static readonly byte[] Secret = "the-client-secret"u8.ToArray();

    private static readonly byte[] MailServer = "the-mail-server-key"u8.ToArray();

    /// <summary>
    /// CONV-DESIGN-007: every secret held through the filling is one the ring lends once
    /// the filling marks it filled, and the mail server's once it marks it complete.
    /// </summary>
    [Fact]
    public void Fill_AfterEachSecretIsHeld_TheRingLendsEachAndTheMailServersOnceCompleted()
    {
        var ring = new KeyRing();
        var filling = new KeyRingFilling(ring, new MailServerInUse());

        filling.HoldKeyEncryptionKeys(new KeyEncryptionKeys(1, Versions(Wrapping)));
        filling.HoldFingerprintKeys(new FingerprintKeys(1, Versions(Fingerprint)));
        filling.HoldMaintenanceCredential(Maintenance);
        filling.Hold("google", ProviderCredential.Secret(Secret));
        filling.Fill();
        filling.HoldMailServerSecret(MailServer);
        filling.Completed();

        Assert.Equal(Wrapping, ring.BorrowKeyEncryptionKey(1, key => key.ToArray()).Match(read => read, _ => []));
        Assert.Equal(
            Fingerprint,
            ring.BorrowFingerprintKeys(keys => keys.Versions[1].ToArray()).Match(read => read, _ => []));
        Assert.Equal(Maintenance, ring.BorrowMaintenanceCredential(held => held.ToArray()).Match(read => read, _ => []));
        Assert.Equal(
            Secret,
            ring.BorrowProviderCredential("google", credential => credential.Material.ToArray())
                .Match(read => read, _ => []));
        Assert.Equal(MailServer, ring.BorrowMailServerSecret(held => held.ToArray()).Match(read => read, _ => []));
    }

    /// <summary>
    /// CONV-CODE-007 AC3: a read before the filling marks the ring filled is a fault, as
    /// is a read of the mail server's key before it marks the ring complete.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_AC3_AReadBeforeTheStepThatReadsTheSecretIsDoneThrows()
    {
        var ring = new KeyRing();
        var filling = new KeyRingFilling(ring, new MailServerInUse());

        filling.HoldMaintenanceCredential(Maintenance);

        Assert.Throws<InvalidOperationException>(() => ring.BorrowMaintenanceCredential(held => held.Length));

        filling.Fill();

        Assert.Equal(Maintenance.Length, ring.BorrowMaintenanceCredential(held => held.Length).Match(read => read, _ => 0));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowMailServerSecret(held => held.Length));
    }

    /// <summary>
    /// CONV-CODE-007 AC3: the clearing asked through the filling leaves every array of
    /// the ring zero, and a read after it is a fault.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_AC3_TheClearingLeavesEveryArrayOfTheRingZero()
    {
        var ring = new KeyRing();
        var filling = new KeyRingFilling(ring, new MailServerInUse());

        filling.HoldMaintenanceCredential(Maintenance);
        filling.Hold("google", ProviderCredential.Secret(Secret));
        filling.Fill();

        filling.Clear();

        Assert.Equal(2, ring.Arrays.Count());
        Assert.All(ring.Arrays, array => Assert.All(array, value => Assert.Equal(0, value)));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowMaintenanceCredential(held => held.Length));
    }

    /// <summary>
    /// CONV-DESIGN-007 AC5: the choice recorded through the filling is the one the mail
    /// server in use answers, a read before it is a fault, and it is recorded once.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC5_TheChoiceRecordedIsTheOneTheMailServerInUseAnswers()
    {
        var inUse = new MailServerInUse();
        var filling = new KeyRingFilling(new KeyRing(), inUse);
        var server = new Unreached();

        Assert.Throws<InvalidOperationException>(() => inUse.Chosen());

        filling.Choose(server);

        Assert.Same(server, inUse.Chosen().Match<IMailServer?>(chosen => chosen, _ => null));
        Assert.Throws<InvalidOperationException>(() => filling.Choose(server: null));
    }

    private static Dictionary<int, ReadOnlyMemory<byte>> Versions(byte[] key) => new() { [1] = key };

    // A mail server the tests choose and never call.
    private sealed class Unreached : IMailServer
    {
        public ValueTask<Result> ProvisionAsync(MailboxPush push, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The mail server is not called here.");

        public ValueTask<Result<IReadOnlyList<HostedMailbox>>> MailboxesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The mail server is not called here.");

        public ValueTask<Result<IReadOnlyList<AppPassword>>> AppPasswordsAsync(
            string accessToken,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The mail server is not called here.");

        public ValueTask<Result<IssuedAppPassword>> CreateAppPasswordAsync(
            string accessToken,
            string label,
            DateTimeOffset? expiresAt,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The mail server is not called here.");

        public ValueTask<Result> RevokeAppPasswordAsync(
            string accessToken,
            AppPasswordId id,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The mail server is not called here.");
    }
}
