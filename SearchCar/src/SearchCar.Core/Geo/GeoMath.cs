using SearchCar.Core.Models;

namespace SearchCar.Core.Geo;

public static class GeoMath
{
    private const double EarthRadiusKm = 6371.0088;

    /// <summary>Great-circle distance in kilometres (haversine).</summary>
    public static double DistanceKm(GeoPoint a, GeoPoint b)
    {
        var dLat = ToRad(b.Latitude - a.Latitude);
        var dLon = ToRad(b.Longitude - a.Longitude);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(a.Latitude)) * Math.Cos(ToRad(b.Latitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>Point reached from <paramref name="start"/> after travelling <paramref name="distanceKm"/> on the given bearing.</summary>
    public static GeoPoint Destination(GeoPoint start, double bearingDeg, double distanceKm)
    {
        var delta = distanceKm / EarthRadiusKm;
        var theta = ToRad(bearingDeg);
        var phi1 = ToRad(start.Latitude);
        var lambda1 = ToRad(start.Longitude);

        var phi2 = Math.Asin(Math.Sin(phi1) * Math.Cos(delta) + Math.Cos(phi1) * Math.Sin(delta) * Math.Cos(theta));
        var lambda2 = lambda1 + Math.Atan2(Math.Sin(theta) * Math.Sin(delta) * Math.Cos(phi1),
                                           Math.Cos(delta) - Math.Sin(phi1) * Math.Sin(phi2));
        return new GeoPoint(ToDeg(phi2), (ToDeg(lambda2) + 540) % 360 - 180);
    }

    public static double BearingDeg(GeoPoint from, GeoPoint to)
    {
        var phi1 = ToRad(from.Latitude);
        var phi2 = ToRad(to.Latitude);
        var dLon = ToRad(to.Longitude - from.Longitude);
        var y = Math.Sin(dLon) * Math.Cos(phi2);
        var x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLon);
        return (ToDeg(Math.Atan2(y, x)) + 360) % 360;
    }

    public static string CompassPoint(double bearingDeg)
    {
        // Polish abbreviations: N = pn., E = wsch., S = pd., W = zach.
        string[] points = ["pn.", "pn.-wsch.", "wsch.", "pd.-wsch.", "pd.", "pd.-zach.", "zach.", "pn.-zach."];
        return points[(int)Math.Round(bearingDeg / 45) % 8];
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;
    private static double ToDeg(double rad) => rad * 180 / Math.PI;
}
