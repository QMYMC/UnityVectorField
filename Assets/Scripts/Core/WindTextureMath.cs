using System;

public static class WindTextureMath
{
    public static float GridUvToTextureUv(float gridUv, int textureSize)
    {
        if (textureSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(textureSize));

        return (gridUv * (textureSize - 1) + 0.5f) / textureSize;
    }
}
