using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A group as the deployment holds it: its name and the members it holds directly.
/// </summary>
/// <param name="Id">The identifier a grant and a membership name.</param>
/// <param name="Name">What the group is called.</param>
/// <param name="Members">The accounts and groups it holds directly; theirs are reached through them.</param>
/// <remarks>Implements AUTHZ-GROUP-001 and chapter 09 section 8a.</remarks>
public sealed record DefinedGroup(GroupId Id, string Name, IReadOnlyList<GrantSubject> Members);
