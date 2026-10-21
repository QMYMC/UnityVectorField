using UnityEngine;

/// <summary>
/// 共享色带提供者。
/// Long/short 两套可视化都从这里读取颜色纹理，避免组件之间互相依赖。
/// </summary>
public class WindColorRamp : MonoBehaviour
{
    const int RampResolution = 256;

    [Header("共享色带")]
    [Tooltip("同一实例的 Long / Short 共用；不同实例应各持有自己的色带组件。")]
    public Gradient colorGradient;

    Texture2D _rampTexture;
    public int Revision { get; private set; }

    public void SetGradient(Gradient gradient)
    {
        if (gradient == null) throw new System.ArgumentNullException(nameof(gradient));
        colorGradient = new Gradient { mode = gradient.mode };
        colorGradient.SetKeys(gradient.colorKeys, gradient.alphaKeys);
        RebuildRampTexture();
    }

    /// <summary>烘焙后的色带纹理，供材质直接使用。</summary>
    public Texture2D RampTexture
    {
        get
        {
            EnsureRampTexture();
            return _rampTexture;
        }
    }

    void OnValidate()
    {
        EnsureDefaultGradient();
        // Texture creation is deferred; OnValidate can be invoked from an import worker.
        ++Revision;
        _dirty = true;
    }
    bool _dirty;

    void OnDestroy()
    {
        ReleaseRampTexture();
    }

    void EnsureRampTexture()
    {
        EnsureDefaultGradient();

        if (_rampTexture == null || _dirty)
            RebuildRampTexture();
    }

    void RebuildRampTexture()
    {
        EnsureDefaultGradient();
        _dirty = false;
        ++Revision;
        ReleaseRampTexture();

        _rampTexture = new Texture2D(RampResolution, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var colors = new Color[RampResolution];
        for (int i = 0; i < RampResolution; i++)
            colors[i] = colorGradient.Evaluate(i / (RampResolution - 1f));

        _rampTexture.SetPixels(colors);
        _rampTexture.Apply();
    }

    void EnsureDefaultGradient()
    {
        if (colorGradient != null && colorGradient.colorKeys.Length > 0)
            return;

        colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color32(68, 1, 84, 255), 0.00f),
                new GradientColorKey(new Color32(59, 82, 139, 255), 0.25f),
                new GradientColorKey(new Color32(33, 145, 140, 255), 0.50f),
                new GradientColorKey(new Color32(94, 201, 98, 255), 0.75f),
                new GradientColorKey(new Color32(253, 231, 37, 255), 1.00f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            });
    }

    void ReleaseRampTexture()
    {
        if (_rampTexture == null)
            return;

        if (Application.isPlaying)
            Destroy(_rampTexture);
        else
            DestroyImmediate(_rampTexture);

        _rampTexture = null;
    }
}
