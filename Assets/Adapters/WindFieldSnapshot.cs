using UnityEngine;

public sealed class WindFieldSnapshot
{
    public readonly float[] LongitudeAxis;
    public readonly float[] LatitudeAxis;
    public readonly float[] AltitudeAxis;
    public readonly Vector3[] GridVectors;
    public readonly int SizeX;
    public readonly int SizeY;
    public readonly int SizeZ;
    public readonly bool Is2D;
    public readonly float HorizontalScale;
    public readonly float VerticalScale;

    public WindFieldSnapshot(
        float[] longitudeAxis,
        float[] latitudeAxis,
        float[] altitudeAxis,
        Vector3[] gridVectors,
        int sizeX,
        int sizeY,
        int sizeZ,
        bool is2D,
        float horizontalScale,
        float verticalScale)
    {
        LongitudeAxis = longitudeAxis;
        LatitudeAxis = latitudeAxis;
        AltitudeAxis = altitudeAxis;
        GridVectors = gridVectors;
        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
        Is2D = is2D;
        HorizontalScale = horizontalScale;
        VerticalScale = verticalScale;
    }

    public Vector3 SamplePhysicalVector(Vector3 worldPosition)
    {
        float longitude = worldPosition.x / HorizontalScale + LongitudeAxis[0];
        float latitude = worldPosition.z / HorizontalScale + LatitudeAxis[0];
        float altitude = Is2D ? AltitudeAxis[0] : worldPosition.y / VerticalScale;
        return WindFieldGridMath.Sample(
            GridVectors, SizeX, SizeY, SizeZ,
            LongitudeAxis, LatitudeAxis, AltitudeAxis,
            longitude, latitude, altitude);
    }

    public Vector3 SampleWorldVelocity(Vector3 worldPosition)
    {
        float latitude = worldPosition.z / HorizontalScale + LatitudeAxis[0];
        return WindCoordinateMath.PhysicalToWorldVelocity(
            SamplePhysicalVector(worldPosition), latitude, HorizontalScale, VerticalScale);
    }

    public bool IsInBounds(Vector3 worldPosition)
    {
        float longitude = worldPosition.x / HorizontalScale + LongitudeAxis[0];
        float latitude = worldPosition.z / HorizontalScale + LatitudeAxis[0];
        if (longitude < LongitudeAxis[0] || longitude > LongitudeAxis[SizeX - 1]) return false;
        if (latitude < LatitudeAxis[0] || latitude > LatitudeAxis[SizeY - 1]) return false;
        if (!Is2D)
        {
            float altitude = worldPosition.y / VerticalScale;
            if (altitude < AltitudeAxis[0] || altitude > AltitudeAxis[SizeZ - 1]) return false;
        }
        return true;
    }
}
