namespace Janus.Core;

/// <summary>
/// The error codes of chapter 10 section 1 that the library raises. Each is stable:
/// rewording the message a host renders from a code is free, changing the code is a
/// breaking change to the contract.
/// </summary>
/// <remarks>Implements CONV-NAME-003, LIB-API-001.</remarks>
public static class ErrorCodes
{
    /// <summary>
    /// The setting is not changeable through the application. Change it where the
    /// application cannot reach, by a command on the server or by a redeployment.
    /// </summary>
    /// <remarks>Implements OPS-CFG-004, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationKeyProtected { get; } = ErrorCode.Parse("config.key.protected");

    /// <summary>
    /// The value is below the enforced minimum. Supply a value at or above the floor;
    /// the floor is not clamped to silently.
    /// </summary>
    /// <remarks>Implements OPS-CFG-003, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationValueBelowFloor { get; } = ErrorCode.Parse("config.value.belowfloor");

    /// <summary>
    /// The value is above the enforced maximum. Supply a value at or below the
    /// ceiling.
    /// </summary>
    /// <remarks>Implements AUTH-SESS-005, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationValueAboveCeiling { get; } = ErrorCode.Parse("config.value.aboveceiling");

    /// <summary>
    /// Loosening a control requires step-up authentication and a written reason.
    /// Present both and repeat the change.
    /// </summary>
    /// <remarks>Implements OPS-CFG-002, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationChangeStepUpRequired { get; } = ErrorCode.Parse("config.change.stepuprequired");

    /// <summary>
    /// An organization policy field is looser than the system default. Tighten the
    /// field, or raise the system default first.
    /// </summary>
    /// <remarks>Implements AUTH-STEP-002a, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationPolicyBelowSystem { get; } = ErrorCode.Parse("config.policy.belowsystem");

    /// <summary>
    /// Startup: the governing language of legal documents is unset. Supply
    /// <c>legal.governinglanguage</c>; it has no default and cannot be guessed for a
    /// deployment.
    /// </summary>
    /// <remarks>Implements PRIV-CONS-005, LIB-HOST-001, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupGoverningLanguage { get; } = ErrorCode.Parse("model.startup.governinglanguage");
}
