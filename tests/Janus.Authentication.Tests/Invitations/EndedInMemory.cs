using System;
using Janus.Core;

namespace Janus.Authentication.Tests.Invitations;

/// <summary>
/// One membership an administrator ended, as the fake keeps it.
/// </summary>
/// <param name="Id">The membership.</param>
/// <param name="Subject">Whose.</param>
/// <param name="Organization">Of which organization.</param>
/// <param name="At">When it ended.</param>
internal sealed record EndedInMemory(
    MembershipId Id,
    SubjectId Subject,
    OrganizationId Organization,
    DateTimeOffset At);
