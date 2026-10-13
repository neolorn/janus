using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core.Configuration;

/// <summary>
/// Where the runtime-changeable keys of chapter 10 section 4 are read from. Every
/// such key is read through this, never from bound options, so a change through the
/// management application takes effect without a restart. It only reads: a change is
/// written through the library's one configuration writer, which asks the step-up, the
/// reason and the record every change carries (OPS-CFG-005, OPS-CFG-008).
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-DESIGN-005, OPS-CFG-001, OPS-CFG-008. A key the
/// deployment never wrote reads as its default; a stored value the key does not
/// admit, which a tightened floor can leave behind, is a fault: the read throws,
/// naming the key and never the stored text, and no default stands in for it.
/// </remarks>
public interface IConfigurationStore
{
    /// <summary>
    /// Reads a key that exists once for the deployment.
    /// </summary>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <param name="setting">The setting, from <see cref="Settings"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The value in force, or the failure where the key is one the deployment has to
    /// name and it named none.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// The stored value does not read under the key's type and constraints.
    /// </exception>
    ValueTask<Result<TValue>> ReadAsync<TValue>(Setting<TValue> setting, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one member of a key that exists once per organization or once per
    /// host-declared category.
    /// </summary>
    /// <typeparam name="TValue">The type of the member's value.</typeparam>
    /// <param name="family">The family, from <see cref="Settings"/>.</param>
    /// <param name="parameter">The organization identifier or the declared category.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The value in force for that member, or the failure where the member has no
    /// value and the family has no default.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// The stored value does not read under the family's type and constraints.
    /// </exception>
    ValueTask<Result<TValue>> ReadAsync<TValue>(SettingFamily<TValue> family, string parameter, CancellationToken cancellationToken);

    /// <summary>
    /// Reads every member of such a key the deployment has written a value for.
    /// </summary>
    /// <typeparam name="TValue">The type of a member's value.</typeparam>
    /// <param name="family">The family, from <see cref="Settings"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The value in force for each member the deployment wrote, by the organization
    /// identifier or the declared category it was written under. A member nobody wrote
    /// is not here: it reads as the family's default.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// A stored value does not read under the family's type and constraints.
    /// </exception>
    /// <remarks>
    /// Implements OPS-CFG-008 and LIB-HOST-001. What a startup check has to know is
    /// whether any member is set at all, which no read of one member can answer.
    /// </remarks>
    ValueTask<Result<IReadOnlyDictionary<string, TValue>>> ReadWrittenAsync<TValue>(
        SettingFamily<TValue> family,
        CancellationToken cancellationToken);
}
