using System;

namespace Janus.Hosting.Bff;

/// <summary>
/// Marks one of the host's controllers, actions or Razor pages as one that answers
/// anyone, where routing reaches it after <see cref="PipelineProfiles.UseBrowserProfile"/>.
/// Every other one needs a live sign-in session, and a request that reaches it without
/// one is refused before it runs.
/// </summary>
/// <remarks>
/// Implements BFF-HOST-002 (D-211). It is read as
/// <see cref="PipelineProfiles.AllowNoSession{TBuilder}"/> is, the nearer mark standing,
/// and serves where no convention builder of the endpoint's own is at hand: an action or a
/// page a dynamic route or a fallback resolves to. A marked endpoint still passes every
/// stage that establishes what the browser carries.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class AllowNoSessionAttribute : Attribute;
