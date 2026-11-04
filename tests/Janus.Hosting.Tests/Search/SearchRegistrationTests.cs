using System.Linq;
using System.Text.Json;
using Janus.Authentication.Search;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Janus.Hosting.Tests.Search;

/// <summary>
/// The staff search as the registration declares it: off with the four-parameter
/// <c>AddJanus</c> and with <see cref="StaffSearch.Off"/>, served with
/// <see cref="StaffSearch.Served"/> (LIB-HOST-001, IDN-SRCH-003, D-256).
/// </summary>
[Trait("kind", "unit")]
public sealed class SearchRegistrationTests
{
    private const string Connection = "Host=nowhere;Database=identity";

    /// <summary>
    /// LIB-HOST-001 AC8: a process registered with the four-parameter <c>AddJanus</c>, or
    /// with <see cref="StaffSearch.Off"/>, answers the search <c>off</c> and keeps no
    /// index; one registered with <see cref="StaffSearch.Served"/> answers
    /// <c>building</c> until its index is built, and runs the one service that builds it.
    /// What a served process does under <c>warn</c> with a check failing is
    /// <c>SearchIndexServiceTests</c>'s.
    /// </summary>
    [Fact]
    public void LIB_HOST_001_AC8_TheRegistrationDeclaresWhetherTheProcessServesTheSearch()
    {
        IServiceCollection plain = new ServiceCollection()
            .AddJanus(Connection, HostFixture.Declaration(), ApplicationKind.Public);
        IServiceCollection off = new ServiceCollection()
            .AddJanus(Connection, HostFixture.Declaration(), ApplicationKind.Public, StaffSearch.Off);
        IServiceCollection served = new ServiceCollection()
            .AddJanus(Connection, HostFixture.Declaration(), ApplicationKind.Public, StaffSearch.Served);

        Assert.Equal("off", Reason(plain));
        Assert.Equal("off", Reason(off));
        Assert.Equal("building", Reason(served));
        Assert.Equal(Hosted(plain), Hosted(off));
        Assert.Equal(Hosted(plain) + 1, Hosted(served));
    }

    private static int Hosted(IServiceCollection services) =>
        services.Count(service => service.ServiceType == typeof(IHostedService));

    private static string? Reason(IServiceCollection services)
    {
        using ServiceProvider provider = services.BuildServiceProvider();

        return provider.GetRequiredService<SearchIndexHolder>().Current().Match<string?>(
            _ => null,
            error => error.Details.TryGetValue("reason", out JsonElement reason) ? reason.GetString() : null);
    }
}
