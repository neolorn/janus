using System;
using Janus.Core;
using Janus.Hosting;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The kite workshop's application: the library composed with the workshop's own
/// declaration, context, handlers and endpoints, and mounted under the workshop's
/// prefix. What a deployment supplies of its own (its secret source, its mail and SMS
/// transports, its web server and its clock) is registered beside it by whoever starts
/// it.
/// </summary>
public static class WorkshopApplication
{
    /// <summary>
    /// Where the application mounts the library and its own endpoints.
    /// </summary>
    public const string Prefix = "/api";

    /// <summary>
    /// Which client of the provider the application is (BFF-SESS-006).
    /// </summary>
    public const string Client = "kite-workshop";

    /// <summary>
    /// The address the application is served at under an origin: the origin and the
    /// prefix.
    /// </summary>
    /// <param name="origin">The origin the application answers at.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentNullException">The origin is absent.</exception>
    public static Uri Address(Uri origin)
    {
        ArgumentNullException.ThrowIfNull(origin);

        return new Uri(origin, Prefix);
    }

    /// <summary>
    /// Registers the application: the library under the workshop's declaration, the
    /// values LIB-HOST-001 asks of a host, the workshop's own context and the source of
    /// each relationship its derivations are over, and its purpose handler, subject-event
    /// subscriber and export contributor.
    /// </summary>
    /// <param name="services">The host's collection.</param>
    /// <param name="origin">The origin the application answers at, where its frontend's pages are.</param>
    /// <param name="connectionString">How the application reaches the deployment's database.</param>
    /// <exception cref="ArgumentNullException">The collection or the origin is absent.</exception>
    /// <exception cref="ArgumentException">The connection string is empty.</exception>
    public static void Register(IServiceCollection services, Uri origin, string connectionString) =>
        Register(services, origin, Client, connectionString);

    /// <summary>
    /// Registers the application as <see cref="Register(IServiceCollection, Uri, string)"/>
    /// does, as a given client of the provider: the authentication application itself
    /// where the client is <see cref="Client"/>, or a second application of the same
    /// deployment, served at an origin of its own, whose sign-on that client is
    /// (BFF-SESS-006, D-229).
    /// </summary>
    /// <param name="services">The host's collection.</param>
    /// <param name="origin">
    /// The origin the deployment's authentication application answers at, where its
    /// frontend's pages are.
    /// </param>
    /// <param name="client">Which client of the provider the application is.</param>
    /// <param name="connectionString">How the application reaches the deployment's database.</param>
    /// <exception cref="ArgumentNullException">The collection or the origin is absent.</exception>
    /// <exception cref="ArgumentException">The client or the connection string is empty.</exception>
    public static void Register(IServiceCollection services, Uri origin, string client, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        string site = origin.GetLeftPart(UriPartial.Authority);

        // LIB-HOST-001: the frontend's pages, its sign-in screen and the provider, which
        // is the authentication application, and which client of the provider this one
        // is.
        _ = services.AddSingleton(new PasskeyAddresses(
            new Uri(origin, "/account/password").AbsoluteUri,
            new Uri(origin, "/account/passkeys/new").AbsoluteUri,
            new Uri(origin, "/account/passkeys").AbsoluteUri));
        _ = services.AddSingleton(new AuthenticationAddresses(
            new Uri(origin, "/signin").AbsoluteUri,
            Address(origin).AbsoluteUri));
        _ = services.AddSingleton(new SignOnClient(client));

        // API-LAND-001: both of the frontend's applications are served at the one origin.
        _ = services.AddSingleton(new LandingOrigins(site, site));

        services.AddJanus(connectionString, WorkshopModel.Declaration(), ApplicationKind.Public);

        // LIB-HOST-001: the workshop's own context, given in a scope, and the source of
        // each relationship a derivation is over, answered from that context.
        DbContextOptions<WorkshopContext> options = new DbContextOptionsBuilder<WorkshopContext>()
            .UseNpgsql(connectionString)
            .Options;

        _ = services.AddScoped(_ => new WorkshopContext(options));
        _ = services.AddSingleton(
            RelationshipSource.Of<WorkshopContext, RepairOwner>(WorkshopModel.Owner, context => context.Owners));
        _ = services.AddSingleton(
            RelationshipSource.Of<WorkshopContext, RepairCollector>(WorkshopModel.Collector, context => context.Collectors));

        // PRIV-RIGHT-001a, PRIV-RIGHT-005b, PRIV-RIGHT-003: the workshop's own work for a
        // change of consent or objection, for an event about a customer, and for an export.
        _ = services.AddSingleton<IPurposeHandler>(provider =>
            new WorkshopPurposes(provider.GetRequiredService<IServiceScopeFactory>()));
        _ = services.AddSingleton<ISubjectEventSubscriber>(provider =>
            new WorkshopSubjectEvents(provider.GetRequiredService<IServiceScopeFactory>()));
        _ = services.AddSingleton<IExportContributor>(provider =>
            new WorkshopExport(provider.GetRequiredService<IServiceScopeFactory>()));

        // IDN-PRIN-001: the workshop's jobs, which whoever starts the application runs in
        // the background.
        _ = services.AddSingleton(provider => new FollowUpCalls(provider.GetRequiredService<IServiceScopeFactory>()));
        _ = services.AddSingleton(provider => new ReadyNotices(provider.GetRequiredService<IServiceScopeFactory>()));
    }

    /// <summary>
    /// Mounts the application: under the prefix, the machine profile, then the browser
    /// profile, then the library's endpoints and the workshop's own (BFF-ORDER-001,
    /// BFF-HOST-001); at the site's root, the two documents of REG-PM-001.
    /// </summary>
    /// <param name="application">The built application.</param>
    /// <exception cref="ArgumentNullException">The application is absent.</exception>
    public static void Mount(WebApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        _ = ((IApplicationBuilder)application).Map(new PathString(Prefix), Mounted);
        _ = ((IApplicationBuilder)application).UseRouting();
        _ = application.MapIdentityWellKnown();
        _ = ((IApplicationBuilder)application).UseEndpoints(_ => { });
    }

    private static void Mounted(IApplicationBuilder mount)
    {
        _ = mount.UseRouting();
        _ = mount.UseMachineProfile();
        _ = mount.UseBrowserProfile();
        _ = mount.UseEndpoints(endpoints =>
        {
            _ = endpoints.MapIdentityEndpoints();
            WorkshopEndpoints.Map(endpoints);
        });
    }
}
