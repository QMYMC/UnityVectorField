using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

public enum VectorFieldTextFormat { LocalXYZ, LongitudeLatitudeAltitude }

/// <summary>Local: x y z vx vy vz valid. Legacy: lon lat altitude east up north.</summary>
public static class VectorFieldText
{
    public static VectorFieldFrame Read(string path, VectorFieldTextFormat format = VectorFieldTextFormat.LocalXYZ,
        float horizontalScale = 1, float verticalScale = 1, CancellationToken cancellation = default)
    {
        var rows = new Dictionary<Vector3, (Vector3 value, bool valid)>();
        foreach (string line in File.ReadLines(path))
        {
            cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
            string[] p = line.Split(new[] { ' ', '\t', ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
            int expected = format == VectorFieldTextFormat.LocalXYZ ? 7 : 6;
            if (p.Length != expected) throw new FormatException("Expected " + expected + " columns.");
            float[] n = p.Take(6).Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            if (format == VectorFieldTextFormat.LocalXYZ && p[6] != "0" && p[6] != "1") throw new FormatException("Validity must be 0 or 1.");
            var pos = format == VectorFieldTextFormat.LocalXYZ ? new Vector3(n[0], n[1], n[2]) : new Vector3(n[0], n[2], n[1]);
            if (!VectorFieldGrid.Finite(pos.x) || !VectorFieldGrid.Finite(pos.y) || !VectorFieldGrid.Finite(pos.z)) throw new FormatException("Coordinates must be finite.");
            if (rows.ContainsKey(pos)) throw new FormatException("Duplicate coordinate.");
            rows.Add(pos, (new Vector3(n[3], n[4], n[5]), p.Length == 6 || p[6] == "1"));
        }
        if (rows.Count == 0) throw new FormatException("No grid samples.");
        float[] x = rows.Keys.Select(p => p.x).Distinct().OrderBy(v => v).ToArray();
        float[] y = rows.Keys.Select(p => p.y).Distinct().OrderBy(v => v).ToArray();
        float[] z = rows.Keys.Select(p => p.z).Distinct().OrderBy(v => v).ToArray();
        var values = new Vector3[checked(x.Length * y.Length * z.Length)];
        var valid = new bool[values.Length];
        for (int iz = 0; iz < z.Length; iz++)
        for (int iy = 0; iy < y.Length; iy++)
        for (int ix = 0; ix < x.Length; ix++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (rows.TryGetValue(new Vector3(x[ix], y[iy], z[iz]), out var row))
            { int index = ix + x.Length * (iy + y.Length * iz); values[index] = row.value; valid[index] = row.valid; }
        }
        var map = VectorFieldMapping.Identity;
        if (format == VectorFieldTextFormat.LongitudeLatitudeAltitude)
        {
            // Equirectangular local tangent plane; intended for small geographic extents.
            float lon0 = x[0], lat0 = z[0];
            float metersPerDegree = Mathf.PI / 180 * 6371000;
            float eastScale = metersPerDegree * Mathf.Max(1e-4f, Mathf.Abs(Mathf.Cos((z[0] + z[z.Length - 1]) * .5f * Mathf.Deg2Rad)));
            x = x.Select(v => (v - lon0) * eastScale).ToArray();
            z = z.Select(v => (v - lat0) * metersPerDegree).ToArray();
            map.positionUnitsToLocal = new Vector3(horizontalScale / eastScale, verticalScale, horizontalScale / metersPerDegree);
        }
        return new VectorFieldFrame(new VectorFieldGrid(x, y, z, values, valid), map);
    }
}
