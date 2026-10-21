using NUnit.Framework;
using UnityEngine;

public sealed class WindFieldSnapshotTests
{
    [Test]
    public void Snapshot_SamplesCapturedGridAfterOwnerReassignsItsReferences()
    {
        float[] lon = { 0f, 1f };
        float[] lat = { 0f, 1f };
        float[] alt = { 0f };
        Vector3[] values =
        {
            new Vector3(2f, 0f, 0f), new Vector3(4f, 0f, 0f),
            new Vector3(2f, 0f, 0f), new Vector3(4f, 0f, 0f),
        };
        var snapshot = new WindFieldSnapshot(
            lon, lat, alt, values, 2, 2, 1, true, 100f, 0.001f);

        lon = null;
        lat = null;
        alt = null;
        values = null;

        Assert.That(snapshot.SamplePhysicalVector(new Vector3(50f, 0f, 50f)).x,
            Is.EqualTo(3f).Within(1e-6f));
    }
}
