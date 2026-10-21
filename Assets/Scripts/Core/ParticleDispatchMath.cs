using System;

public static class ParticleDispatchMath
{
    public static int ThreadGroupCount(int particleCount, int threadsPerGroup)
    {
        if (particleCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(particleCount));
        if (threadsPerGroup <= 0)
            throw new ArgumentOutOfRangeException(nameof(threadsPerGroup));

        return (particleCount + threadsPerGroup - 1) / threadsPerGroup;
    }
}
