using System;
using System.Collections.Generic;
using UnityEngine;

public static class StreamlineIntegrator
{
    public static Vector3 Rk4Step(
        Vector3 position,
        float stepSize,
        float direction,
        Func<Vector3, Vector3> sampleWorldVelocity)
    {
        Vector3 k1 = SampleDirection(position, direction, sampleWorldVelocity);
        if (k1.sqrMagnitude < 1e-12f)
            return position;

        Vector3 k2 = SampleDirection(position + 0.5f * stepSize * k1, direction, sampleWorldVelocity);
        Vector3 k3 = SampleDirection(position + 0.5f * stepSize * k2, direction, sampleWorldVelocity);
        Vector3 k4 = SampleDirection(position + stepSize * k3, direction, sampleWorldVelocity);
        return position + (stepSize / 6f) * (k1 + 2f * k2 + 2f * k3 + k4);
    }

    public static List<Vector3> IntegrateBidirectional(
        Vector3 seed,
        Func<Vector3, Vector3> sampleWorldVelocity,
        Func<Vector3, bool> isInBounds,
        float stepSize,
        int maxStepsPerDirection,
        float minSpeed,
        float maxArcLengthPerDirection,
        float loopCellSize)
    {
        if (sampleWorldVelocity == null)
            throw new ArgumentNullException(nameof(sampleWorldVelocity));
        if (isInBounds == null)
            throw new ArgumentNullException(nameof(isInBounds));
        if (stepSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(stepSize));

        var backward = IntegrateSide(seed, -1f, sampleWorldVelocity, isInBounds,
            stepSize, maxStepsPerDirection, minSpeed, maxArcLengthPerDirection, loopCellSize);
        var forward = IntegrateSide(seed, 1f, sampleWorldVelocity, isInBounds,
            stepSize, maxStepsPerDirection, minSpeed, maxArcLengthPerDirection, loopCellSize);

        backward.Reverse();
        var path = new List<Vector3>(backward.Count + 1 + forward.Count);
        path.AddRange(backward);
        path.Add(seed);
        path.AddRange(forward);
        return path;
    }

    static List<Vector3> IntegrateSide(
        Vector3 seed,
        float direction,
        Func<Vector3, Vector3> sampleWorldVelocity,
        Func<Vector3, bool> isInBounds,
        float stepSize,
        int maxSteps,
        float minSpeed,
        float maxArcLength,
        float loopCellSize)
    {
        var points = new List<Vector3>();
        HashSet<Vector3Int> visited = loopCellSize > 0f ? new HashSet<Vector3Int>() : null;
        if (visited != null)
            visited.Add(Quantize(seed, loopCellSize));

        Vector3 position = seed;
        float arcLength = 0f;
        float minSpeedSquared = minSpeed * minSpeed;

        for (int step = 0; step < maxSteps; step++)
        {
            if (sampleWorldVelocity(position).sqrMagnitude < minSpeedSquared)
                break;

            Vector3 next = Rk4Step(position, stepSize, direction, sampleWorldVelocity);
            float distance = Vector3.Distance(position, next);
            if (distance < 1e-6f || !isInBounds(next))
                break;
            if (arcLength + distance > maxArcLength)
                break;

            if (visited != null && !visited.Add(Quantize(next, loopCellSize)))
                break;

            points.Add(next);
            position = next;
            arcLength += distance;
        }

        return points;
    }

    static Vector3 SampleDirection(
        Vector3 position,
        float direction,
        Func<Vector3, Vector3> sampleWorldVelocity)
    {
        Vector3 velocity = sampleWorldVelocity(position);
        return velocity.sqrMagnitude > 1e-12f ? velocity.normalized * direction : Vector3.zero;
    }

    static Vector3Int Quantize(Vector3 value, float cellSize)
    {
        return new Vector3Int(
            Mathf.FloorToInt(value.x / cellSize),
            Mathf.FloorToInt(value.y / cellSize),
            Mathf.FloorToInt(value.z / cellSize));
    }
}
