using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What a value with rules gives where it was never read (CONV-DESIGN-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class CanonicalValueTests
{
    /// <summary>
    /// CONV-DESIGN-004 AC3: no value type the library reads from text under a rule gives
    /// text through any of its members where it was never read, so a default instance
    /// fails where it is first read and no store has an empty text to write for it.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_004_AC3_NoValueWithRulesGivesTextWhereItWasNeverRead()
    {
        Type[] read = [.. typeof(EmailAddress).Assembly.GetExportedTypes().Where(IsReadFromText)];

        Assert.Superset(
            new HashSet<Type> { typeof(EmailAddress), typeof(PhoneNumber), typeof(Permission) },
            read.ToHashSet());

        foreach (Type type in read)
        {
            object unset = Activator.CreateInstance(type)!;

            Assert.Throws<InvalidOperationException>(unset.ToString);

            foreach (PropertyInfo text in type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.PropertyType == typeof(string)))
            {
                Assert.IsType<InvalidOperationException>(
                    Assert.Throws<TargetInvocationException>(() => text.GetValue(unset)).InnerException);
            }
        }
    }

    // A value with rules is a value type that reads itself from text: its own Parse, or
    // its own TryParse, taking the text first.
    private static bool IsReadFromText(Type type) =>
        type.IsValueType
        && type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Any(method => method.Name is "Parse" or "TryParse"
                && method.GetParameters() is [{ } first, ..] parameters
                && first.ParameterType == typeof(string)
                && (method.ReturnType == type || parameters[^1].ParameterType == type.MakeByRefType()));
}
