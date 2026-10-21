using System;
using UnityEngine;

/// <summary>Owns one Cartesian field and its GPU texture. Public mutations run on the Unity main thread.</summary>
public class WindFieldData : MonoBehaviour
{
    public bool IsLoaded { get; private set; }
    public Texture3D FlowTexture { get; private set; }
    public Bounds SimBounds => Frame == null ? new Bounds() : Frame.Bounds;
    public float MaxSpeed => Frame == null ? 0 : Frame.Grid.MaxSpeed;
    public int LoadVersion { get; private set; }
    public VectorFieldFrame Frame { get; private set; }
    public bool Is2D => Frame != null && Frame.Grid.Is2D;
    public event Action OnLoaded;
    public event Action OnUnloaded;
    public void Load(VectorFieldGrid grid, VectorFieldMapping mapping)
    {
        var frame = new VectorFieldFrame(grid, mapping);
        Unload();
        Apply(frame);
    }
    void Apply(VectorFieldFrame frame)
    {
        var grid = frame.Grid;
        if (!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat) ||
            Mathf.Max(grid.SizeX, Mathf.Max(grid.SizeY, grid.SizeZ)) > SystemInfo.maxTexture3DSize)
            throw new NotSupportedException("Field requires RGBAFloat Texture3D and dimensions within maxTexture3DSize.");
        var texture = new Texture3D(grid.SizeX, grid.SizeY, grid.SizeZ, TextureFormat.RGBAFloat, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, name = "Vector field (owned)" };
        try
        {
            var pixels = new Color[grid.Count];
            for (int z = 0; z < grid.SizeZ; z++)
            for (int y = 0; y < grid.SizeY; y++)
            for (int x = 0; x < grid.SizeX; x++)
            {
                var v = grid.Value(x, y, z);
                pixels[grid.Index(x, y, z)] = new Color(v.x, v.y, v.z, grid.Valid(x, y, z) ? 1 : 0);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
        }
        catch { Destroy(texture); throw; }
        Frame = frame; FlowTexture = texture; IsLoaded = true;
        OnLoaded?.Invoke();
    }
    public void Unload()
    {
        ++LoadVersion;
        IsLoaded = false;
        OnUnloaded?.Invoke(); Frame = null;
        if (FlowTexture != null) Destroy(FlowTexture);
        FlowTexture = null;
    }
    public bool TrySampleLocal(Vector3 position, out Vector3 velocity)
    { velocity = Vector3.zero; return IsLoaded && Frame.TrySample(position, out velocity); }
    public Vector3 SamplePhysicalVector(Vector3 worldPosition) => IsLoaded ? Frame.SamplePhysicalVector(transform.InverseTransformPoint(worldPosition)) : Vector3.zero;
    public Vector3 SampleVector(Vector3 worldPosition) => SamplePhysicalVector(worldPosition);
    public Vector3 SampleWorldVelocity(Vector3 worldPosition) => IsLoaded ? transform.TransformVector(Frame.SampleWorldVelocity(transform.InverseTransformPoint(worldPosition))) : Vector3.zero;
    public bool IsInBounds(Vector3 worldPosition) => IsLoaded && Frame.IsInBounds(transform.InverseTransformPoint(worldPosition));
    void OnDestroy() { Unload(); }
}
