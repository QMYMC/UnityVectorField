using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>Deterministic analytic helix with a valid calm patch and a missing corner. No external data.</summary>
public static class SyntheticVectorField
{
    public static VectorFieldGrid Create(bool deep = false, bool plane = false)
    {
        int nx = 25, ny = plane ? 1 : (deep ? 13 : 7), nz = 25;
        var x = Axis(nx, deep ? -14 : -20, deep ? 14 : 20);
        var y = Axis(ny, deep ? -24 : 0, plane ? 0 : (deep ? 0 : 12));
        var z = Axis(nz, deep ? -9 : -20, deep ? 9 : 20);
        var values = new Vector3[nx * ny * nz]; var valid = new bool[values.Length];
        for (int iz = 0; iz < nz; iz++)
        for (int iy = 0; iy < ny; iy++)
        for (int ix = 0; ix < nx; ix++)
        {
            int i = ix + nx * (iy + ny * iz);
            float px = x[ix], py = y[iy], pz = z[iz];
            values[i] = new Vector3(-pz * .15f + .5f, plane ? 0 : .18f * Mathf.Cos(px * .12f), px * .15f);
            if (ix < 4 && iz > nz - 5) values[i] = Vector3.zero;
            valid[i] = !(ix > nx - 5 && iz < 4);
        }
        return new VectorFieldGrid(x, y, z, values, valid);
    }
    static float[] Axis(int count, float min, float max)
    {
        var result = new float[count];
        for (int i = 0; i < count; i++) result[i] = count == 1 ? min : Mathf.Lerp(min, max, i / (count - 1f));
        return result;
    }
    public static void Write(string path, VectorFieldGrid grid)
    {
        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("# x y z vx vy vz valid; position=m, velocity=m/s; index=x+nx*(y+ny*z)");
            for (int z = 0; z < grid.SizeZ; z++)
            for (int y = 0; y < grid.SizeY; y++)
            for (int x = 0; x < grid.SizeX; x++)
            {
                var p = grid.Position(x, y, z); var v = grid.Value(x, y, z);
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:R} {1:R} {2:R} {3:R} {4:R} {5:R} {6}",
                    p.x, p.y, p.z, v.x, v.y, v.z, grid.Valid(x, y, z) ? 1 : 0));
            }
        }
    }
}
