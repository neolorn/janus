using Janus.Authentication.Configuration;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Cli.Configuration;

/// <summary>
/// Reads a value named on the command line for one organization's member of a protected
/// family, as the family admits it.
/// </summary>
/// <param name="organization">The organization whose member it is.</param>
/// <param name="entered">The value as the command line gave it.</param>
/// <remarks>Implements OPS-CFG-004 and the chapter 10 section 4 value types.</remarks>
internal sealed class ProtectedMemberReading(OrganizationId organization, string entered)
    : ISettingFamilyOperation<Result<ProtectedValue>>
{
    /// <inheritdoc/>
    public Result<ProtectedValue> On<TValue>(SettingFamily<TValue> family) =>
        family.Read(organization.ToString(), entered).Match(
            value => Result.Success<ProtectedValue>(new ProtectedMemberValue<TValue>(family, organization, value)),
            Result.Failure<ProtectedValue>);
}
