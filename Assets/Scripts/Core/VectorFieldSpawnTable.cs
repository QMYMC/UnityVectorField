using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct VectorFieldSpawnCell
{
    public Vector3 min;
    public float cumulativeMeasure;
    public Vector3 size;
    public float padding;
}

/// <summary>Area/volume weighted cells, independent of velocity magnitude. GPU rejects invisible samples.</summary>
public sealed class VectorFieldSpawnTable
{
    public readonly VectorFieldSpawnCell[] Cells;
    public readonly double VisibleMeasure;
    public readonly float CandidateMeasure;
    VectorFieldSpawnTable(VectorFieldSpawnCell[] cells, double visible, float candidate)
    { Cells = cells; VisibleMeasure = visible; CandidateMeasure = candidate; }

    public static bool Visible(Vector3 physical, Color[] ramp, float min, float max, float unit, float threshold)
    {
        float speed = physical.magnitude;
        if (speed < 1e-6f || speed <= threshold * 0.5f) return false;
        float p = Mathf.Clamp01((speed * unit - min) / (max - min)) * (ramp.Length - 1);
        int lo = Mathf.FloorToInt(p);
        return Mathf.Lerp(ramp[lo].a, ramp[Mathf.Min(lo + 1, ramp.Length - 1)].a, p - lo) > 0f;
    }

    public static VectorFieldSpawnTable Build(VectorFieldFrame frame, Color[] ramp, float min, float max,
        float unit, float threshold, CancellationToken cancellation = default)
    {
        var cells = new List<VectorFieldSpawnCell>();
        var grid = frame.Grid;
        double total = 0, visible = 0;
        for (int z = 0; z < grid.SizeZ - 1; z++)
        for (int y = 0; y < Mathf.Max(1, grid.SizeY - 1); y++)
        for (int x = 0; x < grid.SizeX - 1; x++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!grid.CellValid(x, y, z)) continue;
            Vector3 a = frame.Mapping.ToLocal(grid.Position(x, y, z));
            Vector3 b = frame.Mapping.ToLocal(grid.Position(x + 1, grid.Is2D ? y : y + 1, z + 1));
            Vector3 size = b - a;
            double measure = Measure(size, grid.Is2D);
            float maximumSpeed = 0, maximumMotion = 0;
            for (int dz = 0; dz < 2; dz++)
            for (int dy = 0; dy < (grid.Is2D ? 1 : 2); dy++)
            for (int dx = 0; dx < 2; dx++)
            {
                var value = grid.Value(x + dx, y + dy, z + dz);
                maximumSpeed = Mathf.Max(maximumSpeed, value.magnitude);
                maximumMotion = Mathf.Max(maximumMotion, frame.ToLocalVelocity(value).magnitude);
            }
            if (maximumMotion < 1e-6f || maximumSpeed < 1e-6f || maximumSpeed <= threshold * 0.5f) continue;
            bool anyAlpha = false;
            int last = Mathf.Min(ramp.Length - 1, Mathf.CeilToInt(Mathf.Clamp01((maximumSpeed * unit - min) / (max - min)) * (ramp.Length - 1)));
            for (int i = 0; i <= last; i++) anyAlpha |= ramp[i].a > 0f;
            if (!anyAlpha) continue;
            total += measure;
            cells.Add(new VectorFieldSpawnCell { min = a, size = size, cumulativeMeasure = (float)total });
            int hits = 0, samples = grid.Is2D ? 4 : 8;
            for (int dz = 0; dz < 2; dz++)
            for (int dy = 0; dy < (grid.Is2D ? 1 : 2); dy++)
            for (int dx = 0; dx < 2; dx++)
            {
                var p = a + Vector3.Scale(size, new Vector3(.25f + dx * .5f, grid.Is2D ? 0 : .25f + dy * .5f, .25f + dz * .5f));
                var physical = frame.SamplePhysicalVector(p);
                if (frame.ToLocalVelocity(physical).sqrMagnitude > 1e-12f && Visible(physical, ramp, min, max, unit, threshold)) hits++;
            }
            visible += measure * hits / samples;
        }
        return new VectorFieldSpawnTable(cells.ToArray(), visible, (float)total);
    }

    public static double Measure(Vector3 size, bool is2D) => (double)size.x * size.z * (is2D ? 1d : size.y);
    public static int ResolveCount(bool automatic, int fixedNumber, int fixedUnit, double visibleMeasure,
        bool is2D, Vector3 referenceSize, int referenceCount, int maximum)
    {
        if (maximum <= 0) return 0;
        if (!automatic) return (int)Math.Min(maximum, Math.Max(0L, (long)fixedNumber * Math.Max(1, fixedUnit)));
        double reference = Measure(referenceSize, is2D);
        if (reference <= 0 || double.IsNaN(reference) || double.IsInfinity(reference) || visibleMeasure <= 0 || referenceCount <= 0) return 0;
        return (int)Math.Min(maximum, Math.Max(1d, Math.Round(visibleMeasure / reference * referenceCount)));
    }
}
