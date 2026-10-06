using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// The Public Suffix List the package carries, which says where a host's registrable
/// domain begins.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-010 and AUTH-FACT-012. The list travels unmodified, header and
/// date included, and both its ICANN and its private sections apply, as browsers apply
/// them. It judges only the deployment's own configured origins: a browser applies its
/// own current list to every ceremony, so an aged copy cannot weaken one. Every name it
/// compares, a rule's and a host's alike, is in the ASCII form the library's own
/// conversion gives it under the checks of REG-DOM-001, never the machine's, so a
/// registrable domain is the same on every machine.
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
        FrozenSet<string> exceptions,
        IReadOnlyList<string> setAside)
    {
        _rules = rules;
        _wildcards = wildcards;
        _exceptions = exceptions;
        SetAside = setAside;
    }

    /// <summary>
    /// The list the package carries.
    /// </summary>
    public static PublicSuffixList Shipped { get; } = Read(
        typeof(PublicSuffixList).Assembly.GetManifestResourceStream(Resource)
        ?? throw new InvalidOperationException("The package carries no Public Suffix List."));

    /// <summary>
    /// The rules set aside when the list was read, the conversion having refused a
    /// label of each: every one as the list writes it, in the order the list holds
    /// them. They are fixed when the list is committed, so the test of the release pins
    /// them by name and nothing reports them at run time.
    /// </summary>
    public IReadOnlyList<string> SetAside { get; }

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
        List<string> setAside = [];

        using StreamReader reading = new(list);

        while (reading.ReadLine() is string line)
        {
            // A rule is the text of the line up to its first white space.
            string rule = line.Trim().Split((char[]?)null, 2)[0];

            if (rule.Length == 0 || rule.StartsWith(Comment, StringComparison.Ordinal))
            {
                continue;
            }

            (HashSet<string> kind, string name) = rule[0] is Excepted
                ? (exceptions, rule[1..])
                : rule.StartsWith(Wildcard, StringComparison.Ordinal)
                    ? (wildcards, rule[Wildcard.Length..])
                    : (rules, rule);

            if (TryAscii(name, out string ascii))
            {
                _ = kind.Add(ascii);
            }
            else
            {
                setAside.Add(rule);
            }
        }

        return new PublicSuffixList(
            rules.ToFrozenSet(StringComparer.Ordinal),
            wildcards.ToFrozenSet(StringComparer.Ordinal),
            exceptions.ToFrozenSet(StringComparer.Ordinal),
            [.. setAside]);
    }

    /// <summary>
    /// The ASCII form the list judges a name by, which is the form every name it
    /// compares is in. A name with none has no registrable domain, and the list answers
    /// nothing about it.
    /// </summary>
    /// <param name="name">The name, a host or an identifier, as it is written.</param>
    /// <param name="ascii">Its ASCII form, or empty.</param>
    /// <returns>Whether the conversion gives it an ASCII form.</returns>
    /// <exception cref="ArgumentNullException">The name is absent.</exception>
    public static bool TryAscii(string name, out string ascii)
    {
        ArgumentNullException.ThrowIfNull(name);

        // The conversion lowers what it reads, so the forms compare as they stand. A
        // name is taken as it is written, without the canonical form a domain lock
        // compares under, so that a deviation character keeps the label a browser
        // resolves it to.
        return CanonicalForm.TryDomainToAscii(name.TrimEnd('.'), out ascii);
    }

    /// <summary>
    /// Whether a host is itself a public suffix, under which anyone may register a name.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <returns>Whether it is one.</returns>
    /// <exception cref="ArgumentException">The host has no ASCII form.</exception>
    public bool IsSuffix(string host)
    {
        ArgumentNullException.ThrowIfNull(host);

        string[] labels = Labels(host);

        return Suffix(labels) == labels.Length;
    }

    /// <summary>
    /// The label immediately before a host's public suffix, which is the name a browser
    /// counts a related-origins allowlist by, in its ASCII form.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <returns>The label, or the host's ASCII form where it is a public suffix.</returns>
    /// <exception cref="ArgumentException">The host has no ASCII form.</exception>
    public string Label(string host)
    {
        ArgumentNullException.ThrowIfNull(host);

        string[] labels = Labels(host);
        int suffix = Suffix(labels);

        return suffix < labels.Length ? labels[labels.Length - suffix - 1] : string.Join('.', labels);
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

    private static string[] Labels(string host) =>
        TryAscii(host, out string ascii)
            ? ascii.Split('.')
            : throw new ArgumentException("The host has no ASCII form to judge it by.", nameof(host));
}
