using NUnit.Framework;

public sealed class WindTextureMathTests
{
    [TestCase(0f, 21, 0.023809524f)]
    [TestCase(1f, 21, 0.976190476f)]
    [TestCase(0.5f, 21, 0.5f)]
    [TestCase(0f, 1, 0.5f)]
    [TestCase(1f, 1, 0.5f)]
    public void GridUvToTextureUv_AlignsGridPointsWithTexelCenters(
        float gridUv,
        int textureSize,
        float expected)
    {
        Assert.That(WindTextureMath.GridUvToTextureUv(gridUv, textureSize),
            Is.EqualTo(expected).Within(1e-6f));
    }
}
