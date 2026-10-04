using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Identity.Identifiers;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Mailboxes;
using Janus.Storage.Authentication.Sending;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// Where the fingerprint key goes when the library's own stores compute fingerprints on
/// a live write: into the fingerprint and nowhere else in the library's schema
/// (INF-HOST-003).
/// </summary>
/// <remarks>
/// The class holds a database of its own, so the scan reads only what its writes left.
/// </remarks>
[Trait("kind", "integration")]
public sealed class FingerprintKeyTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    // The fingerprint key in two versions, of bytes known to the test.
    private static readonly byte[] First = [.. Enumerable.Range(1, 32).Select(at => (byte)at)];

    private static readonly byte[] Second = [.. Enumerable.Range(101, 32).Select(at => (byte)at)];

    private static readonly FingerprintKeys UnderTheFirst = new(
        1,
        new Dictionary<int, ReadOnlyMemory<byte>> { [1] = First });

    private static readonly FingerprintKeys UnderTheSecond = new(
        2,
        new Dictionary<int, ReadOnlyMemory<byte>> { [1] = First, [2] = Second });

    // Every store of the library that computes a fingerprint on a live write, and the
    // lock a value is judged under, as the first test drives each; the rotation's store
    // is driven by the command's test.
    private static readonly Type[] Driven =
    [
        typeof(IdentifierStore),
        typeof(MailboxStore),
        typeof(AuthenticatorStore),
        typeof(ThrottleLedger),
        typeof(NoticeLedger),
        typeof(RegistrationSourceLedger),
        typeof(SendLedger),
        typeof(ValueLock),
    ];

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// INF-HOST-003 AC4: once every store that computes a fingerprint has written under
    /// each version of the key (an identifier of each kind, a mailbox, a provider link,
    /// a throttle failure, a non-existence notice, a registration source and a send under
    /// a restriction), no column of the library's schema holds either version in any of
    /// its five written forms, while the scan finds a fingerprint the writes did store.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_HOST_003_AC4_NoStoreWritesTheFingerprintKeyAsync()
    {
        string firstEmail = await WrittenThroughEveryStoreAsync(UnderTheFirst, 1);
        string secondEmail = await WrittenThroughEveryStoreAsync(UnderTheSecond, 2);

        await using NpgsqlConnection connection = await database.OpenAsync();
        IReadOnlyList<string> holdingTheKey = await FingerprintKeyScan.HoldingAsync(
            connection,
            [.. FingerprintKeyScan.WrittenForms(First), .. FingerprintKeyScan.WrittenForms(Second)]);
        IReadOnlyList<string> holdingAFingerprint = await FingerprintKeyScan.HoldingAsync(
            connection,
            [
                Convert.ToHexStringLower(Fingerprint.Compute(Encoding.UTF8.GetBytes(firstEmail), First)),
                Convert.ToHexStringLower(Fingerprint.Compute(Encoding.UTF8.GetBytes(secondEmail), Second)),
            ]);

        Assert.Empty(holdingTheKey);
        Assert.Contains("identifiers.fingerprint", holdingAFingerprint);
    }

    /// <summary>
    /// INF-HOST-003 AC4: the files of the storage project that compute a fingerprint are
    /// exactly the stores the test above drives and the rotation's store, which the
    /// command's test drives, so a store that begins computing one is scanned too.
    /// </summary>
    [Fact]
    public void INF_HOST_003_AC4_EveryStoreThatComputesAFingerprintIsDriven()
    {
        string source = Path.Combine(Repository.Root, "src", "Janus.Storage");
        string[] computing =
        [
            .. Directory
                .GetFiles(source, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path[source.Length..].Split(Path.DirectorySeparatorChar).Any(folder => folder is "bin" or "obj"))
                .Where(path => File.ReadAllText(path).Contains("Fingerprint.Compute(", StringComparison.Ordinal))
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(
            Driven.Select(type => type.Name).Append(nameof(FingerprintRotationStore)).Order(StringComparer.Ordinal),
            computing);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    // A write through every store that computes a fingerprint, under the given versions,
    // each value its own to the version so no write meets another's row. Hands back the
    // canonical email identifier written.
    private async Task<string> WrittenThroughEveryStoreAsync(FingerprintKeys keys, int version)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var ring = new KeyRingInMemory(_deployment.Keys, keys);
        SubjectId subject = await _deployment.AccountAsync(Noon);
        string tag = version.ToString(CultureInfo.InvariantCulture);

        Assert.True(EmailAddress.TryParse("person" + tag + "@example.test", out EmailAddress email));
        Assert.True(PhoneNumber.TryParse("+44163296001" + tag, out PhoneNumber phone));
        Assert.True(Username.TryParse("kestrel" + tag, out Username username));
        Assert.True(EmailAddress.TryParse("mailbox" + tag + "@example.test", out EmailAddress mailbox));
        Assert.True(CredentialLabel.TryParse("Google", out CredentialLabel label));

        await using StoreContext context = database.Context();
        var identifiers = new IdentifierStore(context, ring, _deployment.Randomness);
        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken);

        set.Add(Identifier.Email(IdentifierId.New(TimeProvider.System), subject, email, email.Value, Noon), maximum: 5);
        set.Add(Identifier.Phone(IdentifierId.New(TimeProvider.System), subject, phone, phone.Value, Noon), maximum: 5);
        set.Add(Identifier.Username(IdentifierId.New(TimeProvider.System), subject, username, Noon), maximum: 5);

        // The lock keys a value by its fingerprint under every version held and writes
        // no column.
        await using (IDbContextTransaction locking = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            await identifiers.LockValuesAsync(
                [(IdentifierKind.Email, email.Value), (IdentifierKind.Phone, phone.Value), (IdentifierKind.Username, username.Value)],
                cancellationToken);
            await locking.CommitAsync(cancellationToken);
        }

        await identifiers.RecordAsync(set, cancellationToken);
        await new MailboxStore(context, _deployment.DataKey(context), ring, _deployment.Randomness)
            .AddAsync(Mailbox.Reserved(mailbox, Noon), cancellationToken);
        await new AuthenticatorStore(context, ring, _deployment.Randomness).LinkAsync(
            Authenticator.Linked(AuthenticatorId.New(TimeProvider.System), subject, Factor.Google, label, Noon),
            "provider-subject-" + tag,
            cancellationToken);
        await new ThrottleLedger(context, new DataConnections(context), ring)
            .FailedAsync(ThrottleScope.Source, "192.0.2." + tag, standing: 2, Noon, cancellationToken);
        _ = await new NoticeLedger(context, new DataConnections(context), ring)
            .FirstAsync("absent" + tag + "@example.test", Noon, TimeSpan.FromHours(1), cancellationToken);
        await new RegistrationSourceLedger(context, ring).RecordAsync("198.51.100." + tag, Noon, cancellationToken);

        var sends = new SendLedger(context, new DataConnections(context), ring);
        var destination = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, "destination" + tag + "@example.test");

        await sends.RecordAsync(
            RandomNumberGenerator.GetBytes(32),
            [new SendCount(destination, TimeSpan.FromHours(1))],
            [],
            Noon,
            cancellationToken);
        await sends.GrantAsync(destination, credit: 3, cancellationToken);

        _ = await context.SaveChangesAsync(cancellationToken);

        return email.Value;
    }
}
