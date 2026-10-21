using System;
using UnityEngine;

public static class WindFieldGridMath
{
    public static bool TryFindBracket(float[] axis, float value, out int lower, out float t)
    {
        lower = 0;
        t = 0f;
        if (axis == null || axis.Length == 0)
            return false;
        if (axis.Length == 1)
            return true;
        if (value < axis[0] || value > axis[axis.Length - 1])
            return false;

        if (value >= axis[axis.Length - 1])
        {
            lower = axis.Length - 2;
            t = 1f;
            return true;
        }

        int lo = 0;
        int hi = axis.Length - 1;
        while (hi - lo > 1)
        {
            int mid = lo + (hi - lo) / 2;
            if (axis[mid] <= value)
                lo = mid;
            else
                hi = mid;
        }

        float span = axis[lo + 1] - axis[lo];
        if (span <= 0f)
            throw new ArgumentException("Axis values must be strictly increasing.", nameof(axis));

        lower = lo;
        t = Mathf.Clamp01((value - axis[lo]) / span);
        return true;
    }

    public static bool IsUniform(float[] axis, float tolerance = 1e-5f)
    {
        if (axis == null || axis.Length < 2)
            return true;

        float expected = axis[1] - axis[0];
        float allowed = tolerance * Mathf.Max(1f, Mathf.Abs(expected));
        for (int i = 2; i < axis.Length; i++)
        {
            float step = axis[i] - axis[i - 1];
            if (Mathf.Abs(step - expected) > allowed)
                return false;
        }
        return true;
    }

    public static Vector3 Sample(
        Vector3[] values,
        int sizeX,
        int sizeY,
        int sizeZ,
        float[] longitudeAxis,
        float[] latitudeAxis,
        float[] altitudeAxis,
        float longitude,
        float latitude,
        float altitude)
    {
        if (values == null || values.Length != sizeX * sizeY * sizeZ)
            throw new ArgumentException("Grid value count does not match dimensions.", nameof(values));
        if (longitudeAxis == null || longitudeAxis.Length != sizeX ||
            latitudeAxis == null || latitudeAxis.Length != sizeY ||
            altitudeAxis == null || altitudeAxis.Length != sizeZ)
            throw new ArgumentException("Axis lengths do not match grid dimensions.");

        if (!TryFindBracket(longitudeAxis, longitude, out int ix, out float tx) ||
            !TryFindBracket(latitudeAxis, latitude, out int iy, out float ty) ||
            !TryFindBracket(altitudeAxis, altitude, out int iz, out float tz))
            return Vector3.zero;

        int ix1 = sizeX > 1 ? ix + 1 : ix;
        int iy1 = sizeY > 1 ? iy + 1 : iy;
        int iz1 = sizeZ > 1 ? iz + 1 : iz;

        Vector3 c000 = Get(values, sizeX, sizeY, ix, iy, iz);
        Vector3 c100 = Get(values, sizeX, sizeY, ix1, iy, iz);
        Vector3 c010 = Get(values, sizeX, sizeY, ix, iy1, iz);
        Vector3 c110 = Get(values, sizeX, sizeY, ix1, iy1, iz);
        Vector3 c001 = Get(values, sizeX, sizeY, ix, iy, iz1);
        Vector3 c101 = Get(values, sizeX, sizeY, ix1, iy, iz1);
        Vector3 c011 = Get(values, sizeX, sizeY, ix, iy1, iz1);
        Vector3 c111 = Get(values, sizeX, sizeY, ix1, iy1, iz1);

        Vector3 z0 = Vector3.Lerp(
            Vector3.Lerp(c000, c100, tx),
            Vector3.Lerp(c010, c110, tx), ty);
        Vector3 z1 = Vector3.Lerp(
            Vector3.Lerp(c001, c101, tx),
            Vector3.Lerp(c011, c111, tx), ty);
        return Vector3.Lerp(z0, z1, tz);
    }

    static Vector3 Get(Vector3[] values, int sizeX, int sizeY, int x, int y, int z)
    {
        return values[x + sizeX * (y + sizeY * z)];
    }
}
