using System;
using Janus.Core;

namespace Janus.Authorization.Tests;

/// <summary>
/// A second host's domain, sharing nothing with the first: a depot owning vehicles,
/// and journeys made in them.
/// </summary>
/// <remarks>
/// AUTHZ-MODEL-001 AC2: two projects with entirely different domains declare against
/// the same library binary, which is what having no host type name in the library
/// buys.
/// </remarks>
internal static class OtherDomain
{
    /// <summary>
    /// The outermost container, which names the organization owning it.
    /// </summary>
    internal sealed class Depot
    {
        /// <summary>The depot.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>The organization owning it.</summary>
        public Guid OrganizationId { get; init; }
    }

    /// <summary>
    /// A vehicle kept at a depot, which may name the person keeping it.
    /// </summary>
    internal sealed class Vehicle
    {
        /// <summary>The vehicle.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>The depot it is kept at.</summary>
        public string DepotId { get; init; } = string.Empty;

        /// <summary>The person keeping it, where one is named.</summary>
        public SubjectId Keeper { get; init; }
    }

    /// <summary>
    /// A journey made in a vehicle.
    /// </summary>
    internal sealed class Journey
    {
        /// <summary>The journey.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>The vehicle it was made in.</summary>
        public string VehicleId { get; init; } = string.Empty;
    }

    /// <summary>
    /// A depot of one organization.
    /// </summary>
    /// <param name="organization">The organization owning it.</param>
    /// <returns>The depot.</returns>
    public static Depot NewDepot(Guid organization) =>
        new() { Id = Named(), OrganizationId = organization };

    /// <summary>
    /// A vehicle kept at a depot.
    /// </summary>
    /// <param name="depot">The depot it is kept at.</param>
    /// <param name="keeper">The person keeping it.</param>
    /// <returns>The vehicle.</returns>
    public static Vehicle NewVehicle(string depot, SubjectId keeper) =>
        new() { Id = Named(), DepotId = depot, Keeper = keeper };

    /// <summary>
    /// A journey made in a vehicle.
    /// </summary>
    /// <param name="vehicle">The vehicle it was made in.</param>
    /// <returns>The journey.</returns>
    public static Journey NewJourney(string vehicle) =>
        new() { Id = Named(), VehicleId = vehicle };

    /// <summary>
    /// A declaration of the whole domain, valid as it stands.
    /// </summary>
    /// <returns>The builder.</returns>
    public static AuthorizationDeclarationBuilder Declared() =>
        new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .RetentionFloor("route", TimeSpan.FromDays(365))
            .LawfulBasis(new LawfulBasisDeclaration("contract", false, false, false, false))
            .Permission("journey:read")
            .Relationship<Vehicle>(
                "keeper",
                "vehicle",
                "host.vehicles",
                vehicle => vehicle.Keeper,
                "keeper",
                vehicle => vehicle.Id,
                "id")
            .Resource<Depot>("depot", depot => depot
                .BelongsToOrganization()
                .Purpose("haulage", "contract", data: ["identity", "route"], subjects: ["drivers"]))
            .Resource<Vehicle>("vehicle", vehicle => vehicle
                .ContainedIn("depot")
                .Purpose("haulage", "contract", data: ["identity", "route"], subjects: ["drivers"])
                .Derivation("keeper", "keeper"))
            .Resource<Journey>("journey", journey => journey
                .ContainedIn("vehicle")
                .Purpose("haulage", "contract", data: ["identity", "route"], subjects: ["drivers"]));

    // A record is named by the host's own text, whatever the host makes that of.
    private static string Named() => Guid.CreateVersion7().ToString();
}
