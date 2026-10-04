using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The HTTP endpoints as a contract: method, path, body members, statuses and codes of
/// each, and the browser profile's stage order (LIB-API-001).
/// </summary>
[Trait("kind", "contract")]
public sealed class EndpointContractTests : IAsyncDisposable
{
    private readonly Deployment _deployment = new();

    /// <summary>
    /// LIB-API-001 AC2: what the endpoint data source says of every endpoint is the
    /// committed contract file, so an endpoint added, dropped or moved, a body member
    /// added, dropped, renamed or retyped, a status or a code an endpoint gains or
    /// loses, and a stage of the browser profile added, dropped or reordered each fail
    /// here and carry their version bump.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheEndpointsAreTheContract()
    {
        using AsyncServiceScope scope = _deployment.Scope();

        Assert.Equal(
            File.ReadAllText(Path.Combine(Repository.Root(), "tests", "Janus.Hosting.Tests", "endpoints.txt"))
                .ReplaceLineEndings("\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries),
            EndpointContract.Lines(
                _deployment.Endpoints,
                scope.ServiceProvider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions));
    }

    /// <summary>
    /// LIB-API-001 AC2: every endpoint carries the metadata the contract file is
    /// generated from, a declaration of its codes and each answer it produces, so none
    /// is listed with a status the file cannot state.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_EveryEndpointCarriesWhatTheContractIsGeneratedFrom()
    {
        Assert.All(
            _deployment.Endpoints.OfType<RouteEndpoint>().Where(endpoint => EndpointDeclaration.Of(endpoint) is not null),
            endpoint => Assert.True(
                endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>().Count > 0,
                $"{endpoint.RoutePattern.RawText} produces nothing."));
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _deployment.DisposeAsync();
}
