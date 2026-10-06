using System;
using Janus.Core;
using Janus.Hosting.Mailboxes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// How the service that fills the key ring and chooses the mail server in use is put
/// together.
/// </summary>
/// <remarks>
/// Implements CONV-CODE-007, CONV-DESIGN-007, D-171, D-176, D-180 and D-189. The ring
/// and the mail server in use are the core's to register, and both are seams: the
/// service that fills the ring reaches them through the core's contracts and never
/// through what stands behind either. The host's own mail server and its secret source
/// are declarations it may leave out, so the service is made by a factory that asks the
/// container for each.
/// </remarks>
internal static class KeyRingRegistration
{
    /// <summary>
    /// The hosted service that fills the ring and chooses the mail server in use, for the
    /// caller to place among the hosted services.
    /// </summary>
    /// <returns>Its registration, a singleton made by <see cref="Service"/>.</returns>
    public static ServiceDescriptor HostedService() =>
        ServiceDescriptor.Singleton<IHostedService>(Service);

    /// <summary>
    /// Makes the service that fills the ring and chooses the mail server in use.
    /// </summary>
    /// <param name="provider">
    /// The container, asked for the host's own mail server and secret source where it
    /// registered them.
    /// </param>
    /// <returns>The service.</returns>
    /// <exception cref="ArgumentNullException">The container is absent.</exception>
    public static KeyRingService Service(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new KeyRingService(
            provider.GetRequiredService<IKeyRingFilling>(),
            provider.GetRequiredService<IKeyRing>(),
            provider.GetRequiredService<JmapMailServer>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetServices<SocialProvider>(),
            provider.GetService<IMailServer>(),
            provider.GetService<ISecretSource>());
    }
}
