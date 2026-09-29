using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Janus.Authentication.Factors;

/// <summary>
/// The Public Suffix List the package carries, which says where a host's registrable
/// domain begins.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-010 and AUTH-FACT-012. The list travels unmodified, header and
/// date included, and both its ICANN and its private sections apply, as browsers apply
/// them. It judges only the deployment's own configured origins: a browser applies its
/// own current list to every ceremony, so an aged copy cannot weaken one.
/// </remarks>
internal sealed class PublicSuffixList
{
    /// <summary>The resource the list travels as.</summary>
    public const string Resource = "Janus.Authentication.Factors.public_suffix_list.dat";

    private const string Comment = "//";
    private const string Wildcard = "*.";
    private const char Excepted = '!';

    private readonly FrozenSet<string> _rules;
    private readonly FrozenSet<string> _wildcards;
    private readonly FrozenSet<string> _exceptions;

    private PublicSuffixList(
        FrozenSet<string> rules,
        FrozenSet<string> wildcards,
        FrozenSet<string> exceptions)
    {
        _rules = rules;
        _wildcards = wildcards;
        _exceptions = exceptions;
    }

    /// <summary>
    /// The list the package carries.
    /// </summary>
    public static PublicSuffixList Shipped { get; } = Read(
        typeof(PublicSuffixList).Assembly.GetManifestResourceStream(Resource)
        ?? throw new InvalidOperationException("The package carries no Public Suffix List."));

    /// <summary>
    /// Reads a list in the form the Public Suffix List is published in.
    /// </summary>
    /// <param name="list">The list, one rule to a line.</param>
    /// <returns>The rules it holds.</returns>
    public static PublicSuffixList Read(Stream list)
    {
        ArgumentNullException.ThrowIfNull(list);

        HashSet<string> rules = new(StringComparer.Ordinal);
        HashSet<string> wildcards = new(StringComparer.Ordinal);
        HashSet<string> exceptions = new(StringComparer.Ordinal);

        using StreamReader reading = new(list);

        while (reading.ReadLine() is string line)
        {
            // A rule is the text of the line up to its first white space.
            string rule = line.Trim().Split((char[]?)null, 2)[0];

            if (rule.Length == 0 || rule.StartsWith(Comment, StringComparison.Ordinal))
            {
                continue;
            }

            if (rule[0] is Excepted)
            {
                _ = exceptions.Add(Normal(rule[1..]));
            }
            else if (rule.StartsWith(Wildcard, StringComparison.Ordinal))
            {
                _ = wildcards.Add(Normal(rule[Wildcard.Length..]));
            }
            else
            {
                _ = rules.Add(Normal(rule));
            }
        }

        return new PublicSuffixList(
            rules.ToFrozenSet(StringComparer.Ordinal),
            wildcards.ToFrozenSet(StringComparer.Ordinal),
            exceptions.ToFrozenSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// Whether a host is itself a public suffix, under which anyone may register a name.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <returns>Whether it is one.</returns>
    public bool IsSuffix(string host)
    {
        ArgumentNullException.ThrowIfNull(host);

        string[] labels = Normal(host).Split('.');

        return Suffix(labels) == labels.Length;
    }

    /// <summary>
    /// The label immediately before a host's public suffix, which is the name a browser
    /// counts a related-origins allowlist by.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <returns>The label, or the host itself where it is a public suffix.</returns>
    public string Label(string host)
    {
        ArgumentNullException.ThrowIfNull(host);

        string[] written = host.TrimEnd('.').Split('.');
        int suffix = Suffix(Normal(host).Split('.'));

        return suffix < written.Length ? written[written.Length - suffix - 1] : host;
    }

    // How many labels from the right the prevailing rule takes as the public suffix:
    // an exception rule prevails and gives up its leftmost label, otherwise the rule
    // with the most labels, and a host no rule names has its last label as its suffix.
    private int Suffix(string[] labels)
    {
        for (int start = 0; start < labels.Length; start++)
        {
            string candidate = string.Join('.', labels[start..]);
            int taken = labels.Length - start;

            if (_exceptions.Contains(candidate))
            {
                return taken - 1;
            }

            if (_rules.Contains(candidate)
                || (taken > 1 && _wildcards.Contains(string.Join('.', labels[(start + 1)..]))))
            {
                return taken;
            }
        }

        return 1;
    }

    // Rules and hosts are compared in the ASCII form a browser resolves a name in, and
    // without regard to case. The mapping carries settings of its own, so none is held.
    private static string Normal(string name) =>
        new IdnMapping().GetAscii(name.TrimEnd('.')).ToUpperInvariant();
}
