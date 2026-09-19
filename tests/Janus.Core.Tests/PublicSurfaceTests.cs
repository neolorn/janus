using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What the shipped code may expose and may hold (CONV-CODE-003, CONV-CODE-004).
/// </summary>
[Trait("kind", "contract")]
public sealed class PublicSurfaceTests
{
    // CONV-CODE-004 AC2 leaves reflection to the model builder. The converter of
    // CONV-ENUM-001 reads each vocabulary's own wire name once, at startup, so that a
    // column's spelling and its check constraint cannot drift from the wire.
    private static readonly string[] ModelBuilder = ["VocabularyConverter.cs"];

    /// <summary>
    /// CONV-CODE-003 AC1: a contract member hands out a read-only view, never a
    /// mutable collection the caller can change under the library.
    /// </summary>
    [Fact]
    public void CONV_CODE_003_AC1_NoContractMemberExposesAMutableCollection()
    {
        IEnumerable<Type> exposed = Members().Where(Mutable);

        Assert.Empty(exposed);
    }

    /// <summary>
    /// CONV-CODE-004 AC1: nothing mutable is shared between requests, so every static
    /// field the library declares is fixed once. The delegate caches the compiler
    /// emits beside a static lambda are not state the library holds, and no source
    /// that uses a lambda can be free of them.
    /// </summary>
    [Fact]
    public void CONV_CODE_004_AC1_NoStaticFieldIsWritableAfterConstruction()
    {
        IEnumerable<FieldInfo> writable = typeof(Result).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(field => !field.IsInitOnly && !field.IsLiteral);

        Assert.Empty(writable);
    }

    /// <summary>
    /// CONV-CODE-004 AC2: reflection belongs to the model builder and to tests; the
    /// shipped code reaches a type or a source generator instead.
    /// </summary>
    [Fact]
    public void CONV_CODE_004_AC2_NoShippedFileUsesReflection()
    {
        IEnumerable<string> reaching = Directory
            .EnumerateFiles(Path.Combine(Repository.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(file => !ModelBuilder.Contains(Path.GetFileName(file), StringComparer.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("System.Reflection", StringComparison.Ordinal));

        Assert.Empty(reaching);
    }

    private static IEnumerable<Type> Members() =>
        typeof(Result).Assembly
            .GetExportedTypes()
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(property => property.PropertyType)
                .Concat(type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))));

    private static bool Mutable(Type type)
    {
        if (type.IsArray)
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        Type definition = type.GetGenericTypeDefinition();

        return definition == typeof(List<>) || definition == typeof(Dictionary<,>);
    }
}
