using NUnit.Framework;
using UnityEngine;

public sealed class WindCoordinateMathTests
{
    [Test]
    public void PhysicalToWorldVelocity_MapsEastUpNorthToXYZ()
    {
        Vector3 east = WindCoordinateMath.PhysicalToWorldVelocity(
            new Vector3(10f, 0f, 0f), 0f, 100f, 0.001f);
        Vector3 up = WindCoordinateMath.PhysicalToWorldVelocity(
            new Vector3(0f, 10f, 0f), 0f, 100f, 0.001f);
        Vector3 north = WindCoordinateMath.PhysicalToWorldVelocity(
            new Vector3(0f, 0f, 10f), 0f, 100f, 0.001f);

        float horizontal = 10f * Mathf.Rad2Deg / WindCoordinateMath.EarthRadiusMeters * 100f;
        Assert.That(east.x, Is.EqualTo(horizontal).Within(1e-7f));
        Assert.That(east.y, Is.Zero); Assert.That(east.z, Is.Zero);
        Assert.That(up.y, Is.EqualTo(0.01f).Within(1e-7f));
        Assert.That(up.x, Is.Zero); Assert.That(up.z, Is.Zero);
        Assert.That(north.z, Is.EqualTo(horizontal).Within(1e-7f));
        Assert.That(north.x, Is.Zero); Assert.That(north.y, Is.Zero);
    }

    [Test]
    public void PhysicalToWorldVelocity_RemainsFiniteNearPole()
    {
        Vector3 value = WindCoordinateMath.PhysicalToWorldVelocity(
            new Vector3(10f, 0f, 0f), 90f, 100f, 0.001f);

        Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
    }
}
