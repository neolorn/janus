using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core.Configuration;

/// <summary>
/// Reading and changing one runtime key of chapter 10 section 4.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, OPS-CFG-002, OPS-CFG-003, OPS-CFG-004, OPS-CFG-005,
/// OPS-CFG-008 and chapter 09 section 8. The named restriction set is not one of the
/// keys served here: it has its own operations and its own permission.
/// </remarks>
public interface IConfigurationAdministration
{
    /// <summary>
    /// One key, for a caller holding <c>config:read</c>.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="key">Which key.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The key, or the refusal: <c>authz.denied</c> without the permission in the
    /// administrative organization, <c>api.request.malformed</c> naming <c>key</c>
    /// where no key served here has that name.
    /// </returns>
    ValueTask<Result<ConfiguredSetting>> ReadAsync(
        AccessContext context,
        ConfigurationKey key,
        CancellationToken cancellationToken);

    /// <summary>
    /// Puts a value in force for one key, for a caller holding <c>config:manage</c>,
    /// and <c>system:administer</c> as well where the change loosens.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="key">Which key.</param>
    /// <param name="value">What it becomes, in the key's own type.</param>
    /// <param name="reason">Why, which every change carries.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>config.key.protected</c>, the value codes of
    /// chapter 10 section 1.5, <c>auth.restriction.reasonrequired</c>, or
    /// <c>auth.stepup.required</c> for a loosening the session has not proved.
    /// </returns>
    ValueTask<Result> ChangeAsync(
        AccessContext context,
        SessionId session,
        ConfigurationKey key,
        JsonElement value,
        string reason,
        CancellationToken cancellationToken);
}
