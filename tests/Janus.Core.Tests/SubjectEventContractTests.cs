using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What the events the outbox delivers may carry: the fact and whose it is, and
/// nothing of whoever consumes them (IDN-LIFE-003a, PRIV-RIGHT-005b).
/// </summary>
[Trait("kind", "unit")]
public sealed class SubjectEventContractTests
{
    /// <summary>
    /// IDN-LIFE-003a AC2: the event says that an account was erased, that a
    /// restriction changed or that an account was taken down, and a second application
    /// registers as another subscriber without the library changing at all.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003a_AC2_NoSubjectEventNamesAConsumerOrItsDomain()
    {
        Assert.Equal(
            ["ErasureRequested", "RestrictionChanged", "TakedownExecuted"],
            Raised().Select(type => type.Name).Order(StringComparer.Ordinal));

        Assert.Equal(
            ["Actor", "Effective", "IdempotencyKey", "RaisedAt", "Reason", "Subject"],
            Carried(typeof(ErasureRequested)));

        Assert.Equal(
            ["Actor", "Effective", "IdempotencyKey", "RaisedAt", "Restricted", "Subject"],
            Carried(typeof(RestrictionChanged)));

        Assert.Equal(
            ["Actor", "Effective", "IdempotencyKey", "RaisedAt", "Subject"],
            Carried(typeof(TakedownExecuted)));
    }

    /// <summary>
    /// PRIV-RIGHT-005b, chapter 10 section 5b: what the host holds reaches an export
    /// when it is assembled, so <c>ExportRequested</c> is retired and no event is owed
    /// after an export.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005b_NoEventIsOwedAfterAnExport() =>
        Assert.DoesNotContain(
            typeof(DomainEvent).Assembly.GetExportedTypes(),
            type => type.IsSubclassOf(typeof(DomainEvent)) && type.Name.StartsWith("Export", StringComparison.Ordinal));

    /// <summary>
    /// IDN-LIFE-003a AC2: every value an event carries is the library's own or the
    /// framework's, so nothing a host declared can reach a second host through one.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003a_AC2_NoSubjectEventCarriesATypeAHostDeclared() =>
        Assert.All(
            Raised().SelectMany(type => type.GetProperties()),
            property => Assert.Contains(
                Underlying(property.PropertyType).Assembly,
                new[] { typeof(Result).Assembly, typeof(string).Assembly }));

    private static IReadOnlyList<Type> Raised() =>
    [
        .. typeof(SubjectEvent).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(SubjectEvent))),
    ];

    private static IReadOnlyList<string> Carried(Type raised) =>
    [
        .. raised
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal),
    ];

    private static Type Underlying(Type carried) => Nullable.GetUnderlyingType(carried) ?? carried;
}
