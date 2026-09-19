namespace Janus.Core.Configuration;

/// <summary>
/// The system policy: the one key of chapter 10 section 4.1 whose value is the policy
/// object of section 4.1a.
/// </summary>
/// <remarks>Implements chapter 10 sections 4.1 and 4.1a, AUTH-PRIN-002.</remarks>
public sealed class PolicySetting : Setting<Policy>
{
    internal PolicySetting(string key, SettingScope scope, Policy fallback)
        : base(key, scope, SettingDirection.AnyChange, required: false, fallback)
    {
    }

    /// <inheritdoc />
    public override Result<Policy> Accept(Policy value) => value is null
        ? Result.Failure<Policy>(Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", "a policy"))
        : Result.Success(value);
}
