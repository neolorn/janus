using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Conformance.Tests;

/// <summary>
/// The conformance suite's sample takes its configuration through the library's
/// operations (LIB-API-002 AC2, D-206).
/// </summary>
/// <param name="host">The sample host.</param>
[Trait("kind", "integration")]
public sealed class SampleConfigurationTests(SampleHost host) : IClassFixture<SampleHost>
{
    /// <summary>
    /// LIB-API-002 AC2: the policy for people in no organization that the sample's first
    /// administrator set at <c>PUT /admin/config/{key}</c> is the one in force, asking at
    /// every gate what the administrative organization's asks.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_002_AC2_TheSamplesPolicyDefaultIsTheOneItsAdministratorSetAsync()
    {
        JsonNode expected = JsonNode.Parse(Settings.PolicyDefault.Write(
            Policies.SystemDefault with { Gates = Policies.AdministrativeOrganization.Gates }))!;

        using HttpResponseMessage read = await host.Administrator.GetAsync(
            new Uri("admin/config/" + Settings.PolicyDefault.Key, UriKind.Relative),
            TestContext.Current.CancellationToken);
        JsonElement setting = await read.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(setting.GetProperty("value").GetRawText())));
    }
}
