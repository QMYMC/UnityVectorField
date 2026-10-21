using System.Runtime.InteropServices;
using NUnit.Framework;

public sealed class WindParticleLayoutTests
{
    [Test]
    public void WindParticle_StrideMatchesThreeShaderStructs()
    {
        Assert.That(Marshal.SizeOf(typeof(WindParticle)), Is.EqualTo(40));
    }
}
