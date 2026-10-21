using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class StreamlineIntegratorTests
{
    [Test]
    public void IntegrateBidirectional_ExtendsBothSidesAndKeepsSeedOnce()
    {
        Vector3 seed = Vector3.zero;
        List<Vector3> path = StreamlineIntegrator.IntegrateBidirectional(
            seed,
            _ => Vector3.right * 12f,
            p => Mathf.Abs(p.x) <= 3.01f,
            1f,
            10,
            0.01f,
            20f,
            0f);

        Assert.That(path[0].x, Is.LessThan(0f));
        Assert.That(path[path.Count - 1].x, Is.GreaterThan(0f));
        Assert.That(path.FindAll(p => p == seed).Count, Is.EqualTo(1));
    }

    [Test]
    public void Rk4Step_PreservesStraightDirectionIndependentOfSpeedMagnitude()
    {
        Vector3 result = StreamlineIntegrator.Rk4Step(
            Vector3.zero, 2f, 1f, _ => new Vector3(30f, 0f, 0f));

        Assert.That(result.x, Is.EqualTo(2f).Within(1e-6f));
        Assert.That(result.y, Is.Zero); Assert.That(result.z, Is.Zero);
    }

    [Test]
    public void IntegrateBidirectional_StopsAtArcLengthLimit()
    {
        List<Vector3> path = StreamlineIntegrator.IntegrateBidirectional(
            Vector3.zero,
            _ => Vector3.forward,
            _ => true,
            1f,
            100,
            0.01f,
            2f,
            0f);

        Assert.That(path.Count, Is.LessThanOrEqualTo(5));
    }

    [Test]
    public void Rk4Step_FollowsTwoDimensionalRotationalField()
    {
        Vector3 position = Vector3.right;
        for (int i = 0; i < 10; i++)
        {
            position = StreamlineIntegrator.Rk4Step(
                position, 0.1f, 1f, p => new Vector3(-p.z, 0f, p.x));
        }

        Assert.That(new Vector2(position.x, position.z).magnitude,
            Is.EqualTo(1f).Within(0.01f));
        Assert.That(position.z, Is.GreaterThan(0.7f));
    }

    [Test]
    public void Rk4Step_FollowsThreeDimensionalHelicalField()
    {
        Vector3 position = Vector3.right;
        for (int i = 0; i < 20; i++)
        {
            position = StreamlineIntegrator.Rk4Step(
                position, 0.1f, 1f, p => new Vector3(-p.z, 0.25f, p.x));
        }

        Assert.That(position.y, Is.GreaterThan(0.2f));
        Assert.That(Mathf.Abs(position.z), Is.GreaterThan(0.5f));
    }
}
