using NUnit.Framework;

public sealed class ParticleDispatchMathTests
{
    [TestCase(1, 1)]
    [TestCase(64, 1)]
    [TestCase(65, 2)]
    [TestCase(100000, 1563)]
    [TestCase(150000, 2344)]
    [TestCase(100001, 1563)]
    public void ThreadGroupCount_CoversEveryParticleWithoutDroppingRemainder(
        int particleCount,
        int expectedGroups)
    {
        Assert.That(
            ParticleDispatchMath.ThreadGroupCount(particleCount, 64),
            Is.EqualTo(expectedGroups));
    }

    [Test]
    public void ThreadGroupCount_RejectsInvalidArguments()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => ParticleDispatchMath.ThreadGroupCount(0, 64));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => ParticleDispatchMath.ThreadGroupCount(1, 0));
    }
}
