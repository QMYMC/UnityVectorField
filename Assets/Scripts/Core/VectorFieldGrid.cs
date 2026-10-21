using System;
using UnityEngine;

/// <summary>Owned Cartesian grid. Index = x + nx * (y + ny * z). A zero vector is valid data.</summary>
public sealed class VectorFieldGrid
{
    readonly float[][] axes;
    readonly Vector3[] vectors;
    readonly bool[] valid;
    public int SizeX => axes[0].Length;
    public int SizeY => axes[1].Length;
    public int SizeZ => axes[2].Length;
    public bool Is2D => SizeY == 1;
    public float MaxSpeed { get; }
    public Vector3 Min => new Vector3(axes[0][0], axes[1][0], axes[2][0]);
    public Vector3 Max => new Vector3(axes[0][SizeX - 1], axes[1][SizeY - 1], axes[2][SizeZ - 1]);
    public int Count => vectors.Length;

    public VectorFieldGrid(float[] x, float[] y, float[] z, Vector3[] values, bool[] validity = null)
    {
        axes = new[] { CopyAxis(x), CopyAxis(y), CopyAxis(z) };
        if (SizeX < 2 || SizeZ < 2)
            throw new ArgumentException("X and Z require at least two coordinates; Y may be a singleton plane.");
        int count = checked(SizeX * SizeY * SizeZ);
        if (values == null || values.Length != count || (validity != null && validity.Length != count))
            throw new ArgumentException("Grid arrays do not match axis lengths.");
        vectors = (Vector3[])values.Clone();
        valid = validity == null ? new bool[count] : (bool[])validity.Clone();
        float maximum = 0f;
        for (int i = 0; i < count; i++)
        {
            if (validity == null) valid[i] = true;
            Vector3 v = vectors[i];
            if (valid[i] && (!Finite(v.x) || !Finite(v.y) || !Finite(v.z) || !Finite(v.magnitude)))
                throw new ArgumentException("Valid vectors must be finite.");
            if (!valid[i]) vectors[i] = Vector3.zero;
            else maximum = Mathf.Max(maximum, v.magnitude);
        }
        MaxSpeed = maximum;
    }

    static float[] CopyAxis(float[] source)
    {
        if (source == null || source.Length == 0) throw new ArgumentException("Axes cannot be empty.");
        var result = (float[])source.Clone();
        for (int i = 0; i < result.Length; i++)
            if (!Finite(result[i]) || (i > 0 && result[i] <= result[i - 1]))
                throw new ArgumentException("Axes must be finite and strictly increasing.");
        return result;
    }

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public float[] CopyAxis(int axis) => (float[])axes[axis].Clone();
    public int Index(int x, int y, int z) => x + SizeX * (y + SizeY * z);
    public Vector3 Position(int x, int y, int z) => new Vector3(axes[0][x], axes[1][y], axes[2][z]);
    public Vector3 Value(int x, int y, int z) => vectors[Index(x, y, z)];
    public bool Valid(int x, int y, int z) => valid[Index(x, y, z)];

    public bool CellValid(int x, int y, int z)
    {
        for (int dz = 0; dz < 2; dz++)
        for (int dy = 0; dy < (Is2D ? 1 : 2); dy++)
        for (int dx = 0; dx < 2; dx++)
            if (!Valid(x + dx, y + dy, z + dz)) return false;
        return true;
    }

    public bool TrySample(Vector3 position, out Vector3 velocity)
    {
        velocity = Vector3.zero;
        if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
            (Is2D && Mathf.Abs(position.y - Min.y) > 1e-4f) ||
            !WindFieldGridMath.TryFindBracket(axes[0], position.x, out int x, out float tx) ||
            !WindFieldGridMath.TryFindBracket(axes[1], position.y, out int y, out float ty) ||
            !WindFieldGridMath.TryFindBracket(axes[2], position.z, out int z, out float tz) ||
            !CellValid(x, y, z)) return false;
        int y1 = Is2D ? y : y + 1;
        velocity = Vector3.Lerp(
            Vector3.Lerp(Vector3.Lerp(Value(x, y, z), Value(x + 1, y, z), tx),
                Vector3.Lerp(Value(x, y1, z), Value(x + 1, y1, z), tx), ty),
            Vector3.Lerp(Vector3.Lerp(Value(x, y, z + 1), Value(x + 1, y, z + 1), tx),
                Vector3.Lerp(Value(x, y1, z + 1), Value(x + 1, y1, z + 1), tx), ty), tz);
        return true;
    }
}

/// <summary>Data positions -> local coordinates. Velocity conversion excludes display speed and Transform.</summary>
[Serializable]
public struct VectorFieldMapping
{
    public Vector3 origin;
    public Vector3 positionUnitsToLocal;
    public float velocityUnitsToPositionUnits;
    public static VectorFieldMapping Identity => new VectorFieldMapping
    { positionUnitsToLocal = Vector3.one, velocityUnitsToPositionUnits = 1f };
    public void Validate()
    {
        if (!VectorFieldGrid.Finite(origin.x) || !VectorFieldGrid.Finite(origin.y) || !VectorFieldGrid.Finite(origin.z) ||
            !VectorFieldGrid.Finite(positionUnitsToLocal.x) || !VectorFieldGrid.Finite(positionUnitsToLocal.y) ||
            !VectorFieldGrid.Finite(positionUnitsToLocal.z) || !VectorFieldGrid.Finite(velocityUnitsToPositionUnits) ||
            positionUnitsToLocal.x <= 0 || positionUnitsToLocal.y <= 0 || positionUnitsToLocal.z <= 0 ||
            velocityUnitsToPositionUnits <= 0)
            throw new ArgumentException("Mapping scales must be positive and finite; origin must be finite.");
    }
    public Vector3 ToLocal(Vector3 position) => Vector3.Scale(position - origin, positionUnitsToLocal);
    public Vector3 ToData(Vector3 position) => origin + new Vector3(position.x / positionUnitsToLocal.x,
        position.y / positionUnitsToLocal.y, position.z / positionUnitsToLocal.z);
    public Vector3 VelocityToLocal(Vector3 velocity) => Vector3.Scale(velocity, positionUnitsToLocal) * velocityUnitsToPositionUnits;
}

public sealed class VectorFieldFrame
{
    public readonly VectorFieldGrid Grid;
    public readonly VectorFieldMapping Mapping;
    public VectorFieldFrame(VectorFieldGrid grid, VectorFieldMapping mapping)
    { Grid = grid ?? throw new ArgumentNullException(nameof(grid)); mapping.Validate(); Mapping = mapping; }
    public bool TrySample(Vector3 localPosition, out Vector3 physical) => Grid.TrySample(Mapping.ToData(localPosition), out physical);
    public Vector3 SamplePhysicalVector(Vector3 localPosition) => TrySample(localPosition, out var v) ? v : Vector3.zero;
    public Vector3 ToLocalVelocity(Vector3 physical)
    {
        var velocity = Mapping.VelocityToLocal(physical);
        if (Grid.Is2D) velocity.y = 0;
        return velocity;
    }
    public Vector3 SampleWorldVelocity(Vector3 localPosition) => ToLocalVelocity(SamplePhysicalVector(localPosition));
    public bool IsInBounds(Vector3 localPosition) => TrySample(localPosition, out _);
    public Bounds Bounds { get { var b = new Bounds(); b.SetMinMax(Mapping.ToLocal(Grid.Min), Mapping.ToLocal(Grid.Max)); return b; } }
}
