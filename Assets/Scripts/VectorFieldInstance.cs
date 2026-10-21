using UnityEngine;

public enum VectorFieldMode { Long, Short }

/// <summary>Optional facade for direct embedding. No global registry or ID-based presets.</summary>
[RequireComponent(typeof(WindFieldData), typeof(WindColorRamp))]
[RequireComponent(typeof(StreamlineSystem), typeof(WindParticleSystem))]
public class VectorFieldInstance : MonoBehaviour
{
    public string instanceLabel = "Vector field";
    public VectorFieldMode mode;
    public bool visible = true;
    public WindFieldData Data => GetComponent<WindFieldData>();
    public WindColorRamp Palette => GetComponent<WindColorRamp>();
    public StreamlineSystem Long => GetComponent<StreamlineSystem>();
    public WindParticleSystem Short => GetComponent<WindParticleSystem>();

    void Awake() { Bind(); SetMode(mode); }
    public void Bind()
    {
        Long.fieldData = Short.fieldData = Data;
        Long.colorRamp = Short.colorRamp = Palette;
    }
    public void Load(VectorFieldGrid grid, VectorFieldMapping mapping)
    { Bind(); Data.Load(grid, mapping); Show(); }
    public void SetMode(VectorFieldMode value)
    {
        mode = value;
        Long.enabled = visible && mode == VectorFieldMode.Long;
        Short.enabled = visible && mode == VectorFieldMode.Short;
    }
    public void Show() { visible = true; SetMode(mode); }
    public void Stop() { visible = false; SetMode(mode); }
    public void Unload() { Stop(); Data.Unload(); }
    public void SetColorRange(float minimum, float maximum, float unitMultiplier = 1)
    {
        Long.minColorSpeed = Short.minColorSpeed = minimum;
        Long.maxColorSpeed = Short.maxColorSpeed = maximum;
        Long.colorSpeedUnitMultiplier = Short.colorSpeedUnitMultiplier = unitMultiplier;
        Short.Rebuild();
    }
    public void SetDisplaySpeed(float multiplier)
    { Long.streamlineSpeed = Short.speedMultiplier = Mathf.Max(0, multiplier); }
    public void SetPalette(Gradient gradient) { Palette.SetGradient(gradient); Short.Rebuild(); }
    public void SetMapping(VectorFieldMapping mapping)
    { if (Data.IsLoaded) Load(Data.Frame.Grid, mapping); }
    void OnValidate()
    {
        if (Application.isPlaying && GetComponent<StreamlineSystem>() != null) SetMode(mode);
    }
}
