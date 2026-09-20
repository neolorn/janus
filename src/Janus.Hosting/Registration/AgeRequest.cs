using System;

namespace Janus.Hosting.Registration;

/// <summary>
/// The age step.
/// </summary>
/// <param name="DateOfBirth">The date of birth, as the person entered it.</param>
/// <remarks>Implements REG-PROF-002.</remarks>
internal sealed record AgeRequest(DateOnly? DateOfBirth);
