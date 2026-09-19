using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Janus.Authentication.Tests.Passwords;

/// <summary>
/// What the library is forbidden to hold or to ask for: a password hint and a
/// knowledge-based recovery question (AUTH-PASS-006).
/// </summary>
[Trait("kind", "contract")]
public sealed class PasswordProhibitionTests
{
    // The vocabulary of the two mechanisms, in the spellings a field, a column or a
    // request member would carry.
    private static readonly string[] Prohibited =
    [
        "hint",
        "securityquestion",
        "securityanswer",
        "recoveryquestion",
        "recoveryanswer",
        "knowledgequestion",
        "knowledgeanswer",
        "secretquestion",
        "secretanswer",
    ];

    /// <summary>
    /// AUTH-PASS-006 AC1: nothing the schema is built from names a hint or a
    /// question, so no column exists for either.
    /// </summary>
    [Fact]
    public void AUTH_PASS_006_AC1_NoHintOrQuestionFieldExistsInTheSchema() =>
        Assert.Empty(Naming(Files(Path.Combine("src", "Janus.Storage"), "*.cs")));

    /// <summary>
    /// AUTH-PASS-006 AC2: nothing on the public surface names one either, so no API
    /// accepts a question or an answer.
    /// </summary>
    [Fact]
    public void AUTH_PASS_006_AC2_NoApiAcceptsASecurityQuestionOrAnswer() =>
        Assert.Empty(Naming(Files("src", "PublicAPI.*.txt")));

    private static string[] Naming(IEnumerable<string> files) =>
    [
        .. files
            .Where(file => File.ReadLines(file).Any(Names))
            .Select(file => Path.GetRelativePath(Root(), file))
            .Order(StringComparer.Ordinal),
    ];

    private static bool Names(string line) =>
        Array.Exists(
            Prohibited,
            word => line.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static string[] Files(string under, string pattern) => Directory.GetFiles(
        Path.Combine(Root(), under),
        pattern,
        SearchOption.AllDirectories);

    private static string Root()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "src")))
        {
            at = at.Parent;
        }

        return at!.FullName;
    }
}
