using NUnit.Framework;
using UnityEngine;

public sealed class WindFieldGridMathTests
{
    [Test]
    public void TryFindBracket_UsesActualNonuniformSpacing()
    {
        Assert.That(WindFieldGridMath.TryFindBracket(
            new[] { 0f, 25f, 75f }, 50f, out int lower, out float t), Is.True);
        Assert.That(lower, Is.EqualTo(1));
        Assert.That(t, Is.EqualTo(0.5f).Within(1e-6f));
    }

    [Test]
    public void TryFindBracket_ClampsMaximumToLastInterval()
    {
        Assert.That(WindFieldGridMath.TryFindBracket(
            new[] { 0f, 25f, 75f }, 75f, out int lower, out float t), Is.True);
        Assert.That(lower, Is.EqualTo(1));
        Assert.That(t, Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void Sample_UsesActualAltitudeSpacing()
    {
        float[] lon = { 0f, 1f };
        float[] lat = { 0f, 1f };
        float[] alt = { 0f, 25f, 75f };
        var values = new Vector3[2 * 2 * 3];

        for (int k = 0; k < alt.Length; k++)
        for (int j = 0; j < lat.Length; j++)
        for (int i = 0; i < lon.Length; i++)
            values[i + 2 * (j + 2 * k)] = new Vector3(lon[i], alt[k], lat[j]);

        Vector3 value = WindFieldGridMath.Sample(
            values, 2, 2, 3, lon, lat, alt, 0.5f, 0.5f, 50f);

        Assert.That(value.x, Is.EqualTo(0.5f).Within(1e-6f));
        Assert.That(value.y, Is.EqualTo(50f).Within(1e-4f));
        Assert.That(value.z, Is.EqualTo(0.5f).Within(1e-6f));
    }

    [Test]
    public void Sample_SupportsSingleLayer2DField()
    {
        float[] lon = { 0f, 1f };
        float[] lat = { 0f, 1f };
        float[] alt = { 200f };
        Vector3[] values =
        {
            new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f),
            new Vector3(0f, 0f, 2f), new Vector3(2f, 0f, 2f),
        };

        Vector3 value = WindFieldGridMath.Sample(
            values, 2, 2, 1, lon, lat, alt, 0.25f, 0.75f, 200f);

        Assert.That(value.x, Is.EqualTo(0.5f).Within(1e-6f));
        Assert.That(value.z, Is.EqualTo(1.5f).Within(1e-6f));
    }

    [Test]
    public void IsUniform_DistinguishesUniformAndNonuniformAxes()
    {
        Assert.That(WindFieldGridMath.IsUniform(new[] { 0f, 25f, 50f }), Is.True);
        Assert.That(WindFieldGridMath.IsUniform(new[] { 0f, 25f, 75f }), Is.False);
        Assert.That(WindFieldGridMath.IsUniform(new[] { 200f }), Is.True);
    }
}
