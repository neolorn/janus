using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The <c>host:&lt;name&gt;</c> key suppliers a deployment registers. A restriction
/// naming a key no supplier answers is refused rather than counted under a value the
/// library invented.
/// </summary>
/// <remarks>Implements LIB-HOST-001, AUTH-ABUSE-004, chapter 10 section 5.14.</remarks>
public sealed class RestrictionKeySuppliers
{
    private readonly Dictionary<string, RestrictionKeySupplier> _declared;

    private RestrictionKeySuppliers(Dictionary<string, RestrictionKeySupplier> declared) =>
        _declared = declared;

    /// <summary>
    /// The deployment that registers none, under which only the built-in keys work.
    /// </summary>
    public static RestrictionKeySuppliers None { get; } = new([]);

    /// <summary>
    /// Every supplier registered.
    /// </summary>
    public IReadOnlyCollection<RestrictionKeySupplier> All => _declared.Values;

    /// <summary>
    /// Takes the registered suppliers as one collection.
    /// </summary>
    /// <param name="declared">What the host registered.</param>
    /// <returns>The collection the library reads.</returns>
    /// <exception cref="ArgumentNullException">The sequence or a member is absent.</exception>
    /// <exception cref="ArgumentException">A name is empty or registered twice.</exception>
    public static RestrictionKeySuppliers Of(IEnumerable<RestrictionKeySupplier> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        var names = new Dictionary<string, RestrictionKeySupplier>(StringComparer.Ordinal);

        foreach (RestrictionKeySupplier supplier in declared)
        {
            ArgumentNullException.ThrowIfNull(supplier);

            if (string.IsNullOrWhiteSpace(supplier.Name))
            {
                throw new ArgumentException(
                    "A restriction key supplier is registered without a name.",
                    nameof(declared));
            }

            if (!names.TryAdd(supplier.Name, supplier))
            {
                throw new ArgumentException(
                    "The restriction key supplier '" + supplier.Name + "' is registered twice.",
                    nameof(declared));
            }
        }

        return new RestrictionKeySuppliers(names);
    }

    /// <summary>
    /// The supplier registered for one name.
    /// </summary>
    /// <param name="name">The name after the colon.</param>
    /// <param name="supplier">What answers that key.</param>
    /// <returns>Whether one is registered.</returns>
    /// <exception cref="ArgumentNullException">The name is absent.</exception>
    public bool TryFind(string name, out RestrictionKeySupplier supplier)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _declared.TryGetValue(name, out supplier!);
    }
}
