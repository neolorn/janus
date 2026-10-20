using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// What a host's record carries of the capabilities its caller holds on it: the actions,
/// never the permissions, and what each still requires, in the order API-CAP-001 writes
/// them, placed by the host itself (BFF-CAP-002, BFF-ORDER-001 stage 10, D-206).
/// </summary>
[Trait("kind", "unit")]
public sealed class CapabilityFieldsTests : IAsyncDisposable
{
    private const string Documents = "/host/documents";

    private const string Written = """{"id":"quarterly","can":["edit","read","share"],"requires":{"edit":["stepup"],"share":["consent","restricted"]}}""";

    // The host writes its records as the framework's options write them.
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly Permission Read = Permission.Parse("document:read");

    private static readonly Permission Edit = Permission.Parse("document:edit");

    private static readonly Permission Share = Permission.Parse("document:share");

    private readonly Deployment _deployment = new(host: MapDocuments);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _deployment.DisposeAsync();

    /// <summary>
    /// BFF-CAP-002 AC3: the fields carry each capability by its action, the actions,
    /// the keys of what they require and each residual list in ordinal order under the
    /// names of chapter 10 section 5.20, and a capability with nothing outstanding
    /// absent from what they require; a host's record written with them carries
    /// <c>can</c> and <c>requires</c> exactly as API-CAP-001 writes them.
    /// </summary>
    [Fact]
    public void BFF_CAP_002_AC3_TheFieldsCarryCanAndRequiresAsApiCap001WritesThem()
    {
        var fields = CapabilityFields.Of(Held());

        Assert.Equal(["edit", "read", "share"], fields.Can);
        Assert.Equal(["edit", "share"], fields.Requires.Keys);
        Assert.Equal(["stepup"], fields.Requires["edit"]);
        Assert.Equal(["consent", "restricted"], fields.Requires["share"]);
        Assert.DoesNotContain("document:read", fields.Can);
        Assert.Equal(Written, JsonSerializer.Serialize(new HostDocument("quarterly", fields.Can, fields.Requires), Web));
    }

    /// <summary>
    /// BFF-CAP-002 (D-206): a capability holding two permissions of one action, which no
    /// type the model starts with declares, is a fault of the calling code rather than
    /// two capabilities under one entry.
    /// </summary>
    [Fact]
    public void BFF_CAP_002_TwoPermissionsOfOneActionAreAFaultOfTheCallingCode()
    {
        var doubled = new Capability(
            ResourceId.Parse("quarterly"),
            new HashSet<Permission> { Read, Permission.Parse("folder:read") },
            new Dictionary<Permission, IReadOnlySet<CapabilityResidual>>());

        Assert.Throws<ArgumentException>(() => CapabilityFields.Of(doubled));
    }

    /// <summary>
    /// BFF-ORDER-001 stage 10: a host endpoint places what <see cref="CapabilityFields"/>
    /// answers on its record itself, and the stage adds nothing to its answer, which is
    /// the record as the host wrote it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_Stage10_TheStageAddsNothingToAHostAnswerAsync()
    {
        var browser = new Browser(_deployment);

        Answer answered = await browser.SendAsync("GET", Documents);

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Equal(Written, answered.Body);
    }

    private static Capability Held() =>
        new(
            ResourceId.Parse("quarterly"),
            new HashSet<Permission> { Share, Read, Edit },
            new Dictionary<Permission, IReadOnlySet<CapabilityResidual>>
            {
                [Share] = new HashSet<CapabilityResidual> { CapabilityResidual.Restricted, CapabilityResidual.Consent },
                [Edit] = new HashSet<CapabilityResidual> { CapabilityResidual.StepUp },
                [Read] = new HashSet<CapabilityResidual>(),
            });

    private static void MapDocuments(IEndpointRouteBuilder endpoints) =>
        _ = endpoints.MapGet(Documents, () =>
        {
            var fields = CapabilityFields.Of(Held());

            return Results.Json(new HostDocument("quarterly", fields.Can, fields.Requires), Web);
        }).AllowNoSession();

    // A host's record, which carries the fields as its members can and requires.
    private sealed record HostDocument(
        string Id,
        IReadOnlyList<string> Can,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Requires);
}
