using System;
using Janus.Core;

namespace Janus.Authentication.Tests.Invitations;

/// <summary>
/// One membership an administrator ended.
/// </summary>
/// <param name="Id">The membership.</param>
/// <param name="Subject">Whose.</param>
/// <param name="Organization">Of which organization.</param>
/// <param name="At">When it ended.</param>
internal sealed record EndedMembership(
    MembershipId Id,
    SubjectId Subject,
    OrganizationId Organization,
    DateTimeOffset At);
