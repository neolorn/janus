using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Identifiers;
using Janus.Privacy.Erasures;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Privacy.Erasures;
using Janus.Storage.Privacy.SubjectKeys;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The hold an erasure leaves on the username its account went by: a row of its own
/// that names no subject and is read without the erased subject's key (REG-IDENT-009).
/// </summary>
[Trait("kind", "integration")]
public sealed class UsernameHoldTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// REG-IDENT-009 AC4: after the erasure the hold is the username's fingerprint and
    /// the instants it runs between, in a table with no column that could name a
    /// subject, and it is read as held although the subject's key is destroyed and the
    /// name resolves to no account.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_009_AC4_TheHoldNamesNoSubjectAndIsReadWithTheSubjectKeyDestroyedAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        Username username = await NamedAsync(subject);

        await EraseAsync(subject);

        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<(string Name, string Type)> columns = await connection.QueryAsync<(string, string)>(
            "SELECT column_name::text, data_type::text FROM information_schema.columns "
                + "WHERE table_schema = 'identity' AND table_name = 'username_holds'");
        (byte[] Fingerprint, DateTime ReleasesAt) hold = await connection.QuerySingleAsync<(byte[], DateTime)>(
            "SELECT fingerprint, releases_at FROM identity.username_holds");

        await using StoreContext reading = database.Context();
        SubjectKeyRecord key = await reading.SubjectKeys
            .SingleAsync(row => row.Id == SubjectKeyId.Of(subject), TestContext.Current.CancellationToken);
        IdentifierStore identifiers = Identifiers(reading);

        Assert.Equal(
            ["fingerprint", "fingerprint_version", "held_from", "releases_at"],
            columns.Select(column => column.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(columns, column => column.Type is "uuid");
        Assert.Equal(Fingerprint.Length, hold.Fingerprint.Length);
        Assert.Equal(
            Noon + Janus.Core.Configuration.Settings.RetentionConsent.Default,
            new DateTimeOffset(hold.ReleasesAt, TimeSpan.Zero));
        Assert.Equal(PersonalDataFormat.ErasedMarker, key.FormatMarker);
        Assert.Null(await identifiers.FindOwnerAsync(
            IdentifierKind.Username,
            username.Value,
            TestContext.Current.CancellationToken));
        Assert.True(await identifiers.IsHeldAsync(
            username.Value,
            Noon.AddHours(1),
            TestContext.Current.CancellationToken));
    }

    private IdentifierStore Identifiers(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);

    private async ValueTask<SubjectId> DeletingAccountAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext deleting = database.Context();
        AccountRecord record = await deleting.Accounts
            .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);
        record.State = AccountState.Deleting;
        record.DeletingBy = DeletionOrigin.Self;
        record.DeletingSince = Noon;
        await deleting.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    // A username of the account's own, chosen before the erasure.
    private async ValueTask<Username> NamedAsync(SubjectId subject)
    {
        Assert.True(Username.TryParse("kestrel" + Guid.NewGuid().ToString("N")[..8], out Username username));

        await using StoreContext writing = database.Context();
        IdentifierStore store = Identifiers(writing);
        IdentifierSet set = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);

        set.Add(
            Identifier.Username(IdentifierId.New(TimeProvider.System), subject, username, Noon),
            maximum: 5);

        await store.RecordAsync(set, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return username;
    }

    private async ValueTask EraseAsync(SubjectId subject)
    {
        await using StoreContext erasing = database.Context();
        await using var work = new UnitOfWork(erasing);

        var eraser = new SubjectEraser(
            erasing,
            new SessionStore(erasing, _deployment.Ring, _deployment.Randomness),
            new ConfigurationStore(erasing, new DataConnections(erasing)),
            new DataConnections(erasing),
            Identifiers(erasing));

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await eraser.EraseAsync(
            subject,
            ErasureReason.ErasureRequest,
            Noon,
            TestContext.Current.CancellationToken);
        await erasing.SaveChangesAsync(TestContext.Current.CancellationToken);
        await work.CommitAsync(TestContext.Current.CancellationToken);
    }
}
