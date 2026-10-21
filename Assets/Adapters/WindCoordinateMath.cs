using System;
using UnityEngine;

public static class WindCoordinateMath
{
    public const float EarthRadiusMeters = 6371000f;
    const double MinimumAbsoluteCosine = 1e-4;

    public static Vector3 PhysicalToWorldVelocity(
        Vector3 eastUpNorthMetersPerSecond,
        float latitudeDegrees,
        float horizontalScale,
        float verticalScale)
    {
        double latitudeRadians = latitudeDegrees * Mathf.Deg2Rad;
        double cosine = Math.Cos(latitudeRadians);
        if (Math.Abs(cosine) < MinimumAbsoluteCosine)
            cosine = cosine < 0d ? -MinimumAbsoluteCosine : MinimumAbsoluteCosine;

        double longitudeDegreesPerMeter = Mathf.Rad2Deg / (EarthRadiusMeters * cosine);
        double latitudeDegreesPerMeter = Mathf.Rad2Deg / EarthRadiusMeters;

        return new Vector3(
            eastUpNorthMetersPerSecond.x * (float)longitudeDegreesPerMeter * horizontalScale,
            eastUpNorthMetersPerSecond.y * verticalScale,
            eastUpNorthMetersPerSecond.z * (float)latitudeDegreesPerMeter * horizontalScale);
    }
}
