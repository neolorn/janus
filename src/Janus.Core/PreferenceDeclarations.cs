using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Janus.Core;

/// <summary>
/// The preference keys a host declares at startup, checked once so that a malformed
/// declaration stops the deployment rather than the first request that meets it.
/// </summary>
/// <remarks>
/// Implements REG-PREF-001 and LIB-HOST-001. A host that declares nothing has no
/// preferences beyond the language and the time zone, which are the library's own.
/// </remarks>
public sealed class PreferenceDeclarations
{
    private readonly Dictionary<string, PreferenceDeclaration> _declared;

    private PreferenceDeclarations(Dictionary<string, PreferenceDeclaration> declared) =>
        _declared = declared;

    /// <summary>
    /// The declarations of a host that declares no preference.
    /// </summary>
    public static PreferenceDeclarations None { get; } = new([]);

    /// <summary>
    /// Every key declared, in the order the host declared it.
    /// </summary>
    public IReadOnlyCollection<PreferenceDeclaration> All => _declared.Values;

    /// <summary>
    /// Reads a host's declarations.
    /// </summary>
    /// <param name="declared">The keys the host declares.</param>
    /// <returns>The declarations.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    /// <exception cref="StartupException">
    /// A key is unnamed, declared twice, names values where its type takes none, names
    /// none where its type takes them, or carries a default its own type refuses.
    /// </exception>
    public static PreferenceDeclarations Of(IEnumerable<PreferenceDeclaration> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        var keys = new Dictionary<string, PreferenceDeclaration>(StringComparer.Ordinal);

        foreach (PreferenceDeclaration declaration in declared)
        {
            ArgumentNullException.ThrowIfNull(declaration);

            if (string.IsNullOrWhiteSpace(declaration.Name))
            {
                throw Malformed(declaration.Name, "a preference is declared without a name");
            }

            if (!keys.TryAdd(declaration.Name, declaration))
            {
                throw Malformed(declaration.Name, "it is declared twice");
            }

            Wellformed(declaration);
        }

        return new PreferenceDeclarations(keys);
    }

    /// <summary>
    /// Finds the declaration of a key.
    /// </summary>
    /// <param name="name">The key.</param>
    /// <param name="declaration">The declaration, or nothing where the host declared no such key.</param>
    /// <returns>Whether the host declared it.</returns>
    /// <exception cref="ArgumentNullException">The key is absent.</exception>
    public bool TryFind(string name, out PreferenceDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _declared.TryGetValue(name, out declaration!);
    }

    private static void Wellformed(PreferenceDeclaration declaration)
    {
        bool choice = declaration.Kind is PreferenceKind.Enum;

        if (choice && (declaration.Choices is null || declaration.Choices.Count == 0))
        {
            throw Malformed(declaration.Name, "its type takes values and it names none");
        }

        if (!choice && declaration.Choices is not null)
        {
            throw Malformed(declaration.Name, "it names values its type does not take");
        }

        if (!declaration.Admits(declaration.Default))
        {
            throw Malformed(declaration.Name, "it defaults to a value its own type refuses");
        }
    }

    // The fault a malformed declaration raises, naming the key it was found on.
    private static StartupException Malformed(string? name, string fault) =>
        new(
            "The preference declaration '" + name + "' is malformed: " + fault + ".",
            new Error(
                ErrorCodes.StartupPreferenceDeclaration,
                new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                {
                    ["preference"] = JsonSerializer.SerializeToElement(name),
                }));
}
