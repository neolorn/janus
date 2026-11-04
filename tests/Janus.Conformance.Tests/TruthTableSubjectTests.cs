using System;
using System.Data.Common;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Conformance.Tests;

/// <summary>
/// Which types a host's truth table may be over: those whose records register with no
/// subject (LIB-TEST-001 AC2, AUTHZ-INHERIT-002, D-257).
/// </summary>
[Trait("kind", "unit")]
public sealed class TruthTableSubjectTests
{
    private static readonly TruthTableCase[] Cases =
    [
        new(TruthTableScenario.GrantOnRecord, SampleHost.ReadSheet, Allowed: true),
    ];

    /// <summary>
    /// LIB-TEST-001 AC2 (D-257): a table over a sensitive type, over a type declaring
    /// <c>Subject</c>, or over a type a container of which is either, is refused before
    /// anything is written, naming the type; no connection is opened for it.
    /// </summary>
    /// <param name="sheet">How the sheet is declared.</param>
    /// <param name="binder">How the binder containing it is declared.</param>
    /// <param name="named">The type the refusal names.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("sensitive", "plain", "sheet")]
    [InlineData("subject", "plain", "sheet")]
    [InlineData("plain", "subject", "binder")]
    [InlineData("plain", "sensitive", "binder")]
    public async Task LIB_TEST_001_AC2_ATableOverATypeWhoseRecordsNeedASubjectIsRefusedNamingItAsync(
        string sheet,
        string binder,
        string named)
    {
        int opened = 0;

        await using ServiceProvider services = Deployed(sheet, binder);

        ArgumentException refused = await Assert.ThrowsAsync<ArgumentException>(
            async () => await ConformanceSuite.TruthTableAsync(
                services,
                deployment: null,
                cancellationToken =>
                {
                    opened++;

                    return ValueTask.FromException<DbConnection>(
                        new InvalidOperationException("A refused table opens no connection."));
                },
                new UnwrittenRows<Sheet>(SampleHost.SheetType),
                Cases,
                TestContext.Current.CancellationToken));

        Assert.Contains("The type " + named + " ", refused.Message, StringComparison.Ordinal);
        Assert.Equal(0, opened);
    }

    // A deployment declaring the sheet, in a binder on a shelf, each as the case says:
    // plain, sensitive, or naming its subject through Subject. The suite reads the
    // declaration as the host built it, before the model would judge the column.
    private static ServiceProvider Deployed(string sheet, string binder) =>
        new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton(_ => RandomNumberGenerator.Create())
            .AddSingleton(new AuthorizationDeclarationBuilder()
                .SensitiveCategory("health")
                .Resource<Shelf>(SampleHost.ShelfType.ToString(), type => type.BelongsToOrganization())
                .Resource<Binder>(SampleHost.BinderType.ToString(), type => Declared(
                    type.ContainedIn(SampleHost.ShelfType.ToString()),
                    binder,
                    held => held.ShelfId))
                .Resource<Sheet>(SampleHost.SheetType.ToString(), type => Declared(
                    type.ContainedIn(SampleHost.BinderType.ToString()),
                    sheet,
                    held => held.BinderId))
                .Build())
            .BuildServiceProvider();

    private static ResourceTypeDeclarationBuilder<TResource> Declared<TResource>(
        ResourceTypeDeclarationBuilder<TResource> type,
        string declared,
        Expression<Func<TResource, object?>> subject) =>
        declared switch
        {
            "sensitive" => type.Sensitive("health"),
            "subject" => type.Subject(subject),
            _ => type,
        };
}
