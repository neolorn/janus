using System;

namespace Janus.Authentication.Sessions;

/// <summary>
/// Where a city lies on the Earth, in decimal degrees.
/// </summary>
/// <param name="Latitude">North of the equator, or south where negative.</param>
/// <param name="Longitude">East of Greenwich, or west where negative.</param>
/// <remarks>
/// Implements OPS-ALERT-007. They are the city's and never the address's, so they say
/// nothing finer than the city a session already shows (AUTH-SESS-013).
/// </remarks>
internal readonly record struct Coordinates(double Latitude, double Longitude)
{
    // The mean radius of the Earth, in kilometres.
    private const double Radius = 6371.0088;

    /// <summary>
    /// The distance to another city over the Earth's surface.
    /// </summary>
    /// <param name="other">The other city.</param>
    /// <returns>The great-circle distance, in kilometres.</returns>
    public double KilometresTo(Coordinates other)
    {
        double latitude = Radians(other.Latitude - Latitude);
        double longitude = Radians(other.Longitude - Longitude);
        double haversine =
            (Math.Sin(latitude / 2) * Math.Sin(latitude / 2))
            + (Math.Cos(Radians(Latitude)) * Math.Cos(Radians(other.Latitude))
                * Math.Sin(longitude / 2) * Math.Sin(longitude / 2));

        return 2 * Radius * Math.Asin(Math.Min(1, Math.Sqrt(haversine)));
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;
}
