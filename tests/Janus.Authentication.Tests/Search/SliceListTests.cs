using System;
using System.Linq;
using System.Reflection;
using Janus.Authentication.Search;
using Xunit;

namespace Janus.Authentication.Tests.Search;

/// <summary>
/// One slice's entries, in name order with spare room (IDN-SRCH-003, D-256).
/// </summary>
[Trait("kind", "unit")]
public sealed class SliceListTests
{
    private static readonly Comparison<int> Numeric = (left, right) => left.CompareTo(right);

    /// <summary>
    /// IDN-SRCH-003 AC11: a built list holds spare room of a hundredth of its length, and
    /// at least one place.
    /// </summary>
    /// <param name="length">How many entries the build added.</param>
    /// <param name="spare">The room it is left with.</param>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1)]
    [InlineData(250, 2)]
    [InlineData(10_000, 100)]
    public void IDN_SRCH_003_AC11_ABuiltListKeepsAHundredthSpareAndAtLeastOnePlace(int length, int spare)
    {
        var list = new SliceList();

        foreach (int entry in Enumerable.Range(0, length))
        {
            list.Append(entry);
        }

        list.Settle();

        Assert.Equal(length + spare, Capacity(list));
    }

    /// <summary>
    /// An entry placed after the build goes where the order puts it, and one taken out
    /// leaves the rest in order.
    /// </summary>
    [Fact]
    public void Insert_AndRemove_KeepTheOrder()
    {
        var list = new SliceList();
        list.Append(10);
        list.Append(30);
        list.Settle();

        list.Insert(20, Numeric);
        list.Insert(5, Numeric);
        list.Insert(40, Numeric);
        list.Remove(30, Numeric);
        list.Remove(99, Numeric);

        Assert.Equal([5, 10, 20, 40], list.Entries.ToArray());
    }

    private static int Capacity(SliceList list) =>
        ((int[])typeof(SliceList).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(list)!).Length;
}
