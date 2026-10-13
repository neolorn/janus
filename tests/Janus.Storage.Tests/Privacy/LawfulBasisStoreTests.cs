using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Privacy.Bases;
using Janus.Storage.Privacy.Bases;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// The table of lawful bases as the start writes it (PRIV-BASIS-001).
/// </summary>
[Trait("kind", "integration")]
public sealed class LawfulBasisStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string InsufficientPrivilege = "42501";

    private static readonly LawfulBasisDeclaration[] Elsewhere =
    [
        new("art-6-1-a", "Consent of the data subject", true, false, false, false),
        new("art-6-1-f", "Legitimate interests", false, false, true, true),
    ];

    /// <summary>
    /// PRIV-BASIS-001 AC6: after a start the table holds exactly the declared bases with
    /// their labels and flags, whatever it held before: a basis no longer declared is
    /// gone, and one declared under another label or other flags reads as declared now.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_BASIS_001_AC6_AfterAStartTheTableHoldsExactlyTheDeclaredBasesAsync()
    {
        await SeededAsync(
        [
            new LawfulBasisDeclaration("consent", "An earlier label", false, false, true, true),
            new LawfulBasisDeclaration("vital-interests", "Vital Interests", false, false, false, false),
        ]);

        await SeededAsync(LawfulBases.Default);

        Assert.Equal(
            LawfulBases.Default.OrderBy(basis => basis.Key, StringComparer.Ordinal),
            await HeldAsync());
    }

    /// <summary>
    /// PRIV-BASIS-001 AC6: two starts with different lists leave one whole list, never a
    /// mixture of the two, however their writes fall.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_BASIS_001_AC6_TwoStartsWithDifferentListsLeaveOneWholeListAsync()
    {
        for (int round = 0; round < 8; round++)
        {
            await Task.WhenAll(
                Task.Run(async () => await SeededAsync(LawfulBases.Default), TestContext.Current.CancellationToken),
                Task.Run(async () => await SeededAsync(Elsewhere), TestContext.Current.CancellationToken));

            IReadOnlyList<LawfulBasisDeclaration> held = await HeldAsync();

            Assert.True(
                held.SequenceEqual(LawfulBases.Default.OrderBy(basis => basis.Key, StringComparer.Ordinal))
                || held.SequenceEqual(Elsewhere),
                "The table holds a mixture of two declared lists.");
        }
    }

    /// <summary>
    /// PRIV-BASIS-001: only the application's runtime credential writes the table; the
    /// maintenance credential neither writes nor reads it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_BASIS_001_OnlyTheRuntimeCredentialWritesTheTableAsync()
    {
        await using NpgsqlConnection runtime = await database.OpenAsync();
        await runtime.ExecuteAsync("SET ROLE identity_app");
        await using NpgsqlConnection maintenance = await database.OpenAsync();
        await maintenance.ExecuteAsync("SET ROLE identity_maintenance");

        await using (NpgsqlTransaction writing = await runtime.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await runtime.ExecuteAsync("LOCK TABLE identity.lawful_bases IN SHARE ROW EXCLUSIVE MODE", transaction: writing);
            await runtime.ExecuteAsync("DELETE FROM identity.lawful_bases WHERE key = 'never-declared'", transaction: writing);
            await writing.RollbackAsync(TestContext.Current.CancellationToken);
        }

        PostgresException written = await Assert.ThrowsAsync<PostgresException>(async () =>
            await maintenance.ExecuteAsync("DELETE FROM identity.lawful_bases"));
        PostgresException read = await Assert.ThrowsAsync<PostgresException>(async () =>
            await maintenance.ExecuteScalarAsync<int>("SELECT count(*)::int FROM identity.lawful_bases"));

        Assert.Equal(InsufficientPrivilege, written.SqlState);
        Assert.Equal(InsufficientPrivilege, read.SqlState);
    }

    private async Task SeededAsync(IReadOnlyList<LawfulBasisDeclaration> declared)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        AuthorizationDeclarationBuilder declaration = new();

        foreach (LawfulBasisDeclaration basis in declared)
        {
            _ = declaration.LawfulBasis(basis);
        }

        await new LawfulBasisSeed(new LawfulBasisStore(new DataConnections(context)), declaration.Build(), work)
            .SeededAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<LawfulBasisDeclaration>> HeldAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        return
        [
            .. (await connection.QueryAsync<(string, string, bool, bool, bool, bool)>(
                """
                SELECT key, label, is_consent, requires_written_consent_for_sensitive, requires_assessment, is_objectable
                FROM identity.lawful_bases
                ORDER BY key COLLATE "C"
                """))
            .Select(row => new LawfulBasisDeclaration(row.Item1, row.Item2, row.Item3, row.Item4, row.Item5, row.Item6)),
        ];
    }
}
