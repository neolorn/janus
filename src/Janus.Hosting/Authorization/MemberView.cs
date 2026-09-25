using System;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// One member of a group as the management application reads it.
/// </summary>
/// <param name="SubjectType">Whether it is one account or a group.</param>
/// <param name="SubjectId">The account or the group.</param>
/// <remarks>Implements chapter 09 section 8a and AUTHZ-GROUP-001.</remarks>
internal sealed record MemberView(SubjectType SubjectType, Guid SubjectId);
