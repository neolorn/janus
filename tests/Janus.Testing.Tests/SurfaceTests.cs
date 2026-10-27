using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Janus.Testing.Tests;

/// <summary>
/// What the testing package reaches: the public surface and nothing under it
/// (LIB-TEST-003, CONV-LAYOUT-002).
/// </summary>
[Trait("kind", "contract")]
public sealed class SurfaceTests
{
    private static readonly Assembly Testing = typeof(FixedSecretSource).Assembly;

    /// <summary>
    /// LIB-TEST-003 AC2: the package references no assembly that writes a library-owned
    /// table, neither the storage area nor a database client, and no assembly it does
    /// reference grants it its internals, so no member of it can write such a table or
    /// call an internal type.
    /// </summary>
    [Fact]
    public void LIB_TEST_003_AC2_NoMemberCanWriteALibraryTableOrCallAnInternalType()
    {
        AssemblyName[] referenced = Testing.GetReferencedAssemblies();

        Assert.DoesNotContain(referenced, name => name.Name is "Janus.Storage" or "Npgsql" or "Dapper"
            || name.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.All(
            referenced,
            name => Assert.DoesNotContain(
                Assembly.Load(name).GetCustomAttributes<InternalsVisibleToAttribute>(),
                granted => granted.AssemblyName.Split(',')[0] == Testing.GetName().Name));
    }
}
