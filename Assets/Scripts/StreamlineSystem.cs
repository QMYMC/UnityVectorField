using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Long mode. Cancellable RK4 integration in local space; each instance owns its render resources.</summary>
public class StreamlineSystem : MonoBehaviour
{
    public WindFieldData fieldData;
    public WindColorRamp colorRamp;
    public Material streamlineMaterial;
    [Header("Integration in local display units")]
    [Min(.001f)] public float stepSize = .2f;
    [Min(2)] public int maxSteps = 1000;
    [Min(0)] public float minVelocityThreshold = .001f;
    [Min(2)] public int minPathLength = 8;
    [Min(0)] public float minSpacing = .7f;
    [Min(0)] public int maxStreamlines = 240;
    [Min(1)] public int seedCandidateMultiplier = 6;
    [Header("Rendering")]
    [Min(1)] public int trailLength = 100;
    [Min(.001f)] public float lineWidth = .055f;
    [Range(0,1)] public float streamlineAlpha = .85f;
    public float headBrightness = 1.2f;
    [Tooltip("Display speed multiplier, independent of physical speed used for colour.")]
    [Min(0)] public float streamlineSpeed = 1;
    public float minColorSpeed;
    public float maxColorSpeed;
    [Min(.000001f)] public float colorSpeedUnitMultiplier = 1;
    [StructLayout(LayoutKind.Sequential)]
    struct Point { public Vector3 position; public float speed; }
    [StructLayout(LayoutKind.Sequential)]
    struct Head { public int startIndex, pointCount; public float headT; }
    ComputeBuffer pointBuffer, headBuffer;
    Material runtimeMaterial;
    Head[] heads;
    float[][] localSpeeds;
    CancellationTokenSource building;
    int generation;
    float builtStep;
    public bool Ready { get; private set; }
    public int ActiveLineCount => Ready ? heads.Length : 0;

    void OnEnable()
    {
        if (fieldData == null) return;
        fieldData.OnLoaded += Rebuild; fieldData.OnUnloaded += Release;
        Rebuild();
    }
    void OnDisable()
    {
        if (fieldData != null) { fieldData.OnLoaded -= Rebuild; fieldData.OnUnloaded -= Release; }
        Release();
    }
    public void Rebuild() { if (isActiveAndEnabled) _ = BuildAsync(); }
    async Task BuildAsync()
    {
        Release();
        if (fieldData == null || !fieldData.IsLoaded || colorRamp == null || streamlineMaterial == null || maxStreamlines <= 0) return;
        var request = new CancellationTokenSource(); building = request;
        var token = request.Token;
        int version = generation;
        var frame = fieldData.Frame;
        float step = Mathf.Max(.001f, stepSize), threshold = Mathf.Max(0, minVelocityThreshold), spacing = Mathf.Max(0, minSpacing);
        int max = Mathf.Max(2, maxSteps), minimum = Mathf.Max(2, minPathLength), limit = maxStreamlines;
        int candidates = Mathf.Min(frame.Grid.Count, (int)Math.Min(int.MaxValue, (long)limit * Mathf.Max(1, seedCandidateMultiplier)));
        try
        {
            List<List<Vector3>> paths = await Task.Run(() =>
            {
                var result = new List<List<Vector3>>();
                var seeds = new List<Vector3>();
                for (int i = 0; i < candidates && result.Count < limit; i++)
                {
                    token.ThrowIfCancellationRequested();
                    // Coprime strides cover layers without clustering seeds on a single edge.
                    int index = (int)(((long)i * 104729 + frame.Grid.Count / 2) % frame.Grid.Count);
                    int x = index % frame.Grid.SizeX, y = index / frame.Grid.SizeX % frame.Grid.SizeY, z = index / (frame.Grid.SizeX * frame.Grid.SizeY);
                    Vector3 seed = frame.Mapping.ToLocal(frame.Grid.Position(x, y, z));
                    if (!frame.TrySample(seed, out var v) || v.magnitude <= threshold) continue;
                    bool close = seeds.Exists(p => (p - seed).sqrMagnitude < spacing * spacing);
                    if (close) continue;
                    Vector3 Sample(Vector3 p)
                    {
                        token.ThrowIfCancellationRequested();
                        return frame.TrySample(p, out var physical) && physical.magnitude > threshold ? frame.ToLocalVelocity(physical) : Vector3.zero;
                    }
                    var path = StreamlineIntegrator.IntegrateBidirectional(seed, Sample, frame.IsInBounds,
                        step, max / 2, 1e-8f, step * (max / 2), step * .45f);
                    if (path.Count < minimum) continue;
                    result.Add(path); seeds.Add(seed);
                }
                return result;
            }, token);
            token.ThrowIfCancellationRequested();
            if (version != generation || !isActiveAndEnabled || fieldData.Frame != frame || paths.Count == 0) return;
            var points = new List<Point>(); heads = new Head[paths.Count]; localSpeeds = new float[paths.Count][];
            for (int i = 0; i < paths.Count; i++)
            {
                heads[i] = new Head { startIndex = points.Count, pointCount = paths[i].Count, headT = (i * .618034f % 1) * (paths[i].Count - 1) };
                localSpeeds[i] = new float[paths[i].Count];
                for (int j = 0; j < paths[i].Count; j++)
                {
                    var v = frame.SamplePhysicalVector(paths[i][j]);
                    points.Add(new Point { position = paths[i][j], speed = v.magnitude });
                    localSpeeds[i][j] = frame.ToLocalVelocity(v).magnitude;
                }
            }
            pointBuffer = new ComputeBuffer(points.Count, 16); pointBuffer.SetData(points);
            headBuffer = new ComputeBuffer(heads.Length, 12); headBuffer.SetData(heads);
            runtimeMaterial = Instantiate(streamlineMaterial); builtStep = step; Ready = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { if (this != null) { Release(); Debug.LogException(exception, this); } }
        finally { if (building == request) building = null; request.Dispose(); }
    }
    void Update()
    {
        if (!Ready) return;
        for (int i = 0; i < heads.Length; i++)
        {
            int index = Mathf.Clamp((int)heads[i].headT, 0, heads[i].pointCount - 1);
            heads[i].headT = (heads[i].headT + localSpeeds[i][index] / builtStep * Mathf.Max(0, streamlineSpeed) * Time.deltaTime) % heads[i].pointCount;
        }
        headBuffer.SetData(heads);
    }
    void OnRenderObject()
    {
        if (!isActiveAndEnabled || !Ready || fieldData == null || !fieldData.IsLoaded) return;
        runtimeMaterial.SetTexture("_ColorRamp", colorRamp.RampTexture);
        runtimeMaterial.SetMatrix("_FieldLocalToWorld", fieldData.transform.localToWorldMatrix);
        runtimeMaterial.SetMatrix("_FieldWorldToLocal", fieldData.transform.worldToLocalMatrix);
        runtimeMaterial.SetBuffer("_PointBuffer", pointBuffer); runtimeMaterial.SetBuffer("_HeadBuffer", headBuffer);
        int trail = Mathf.Clamp(trailLength, 1, 100000);
        runtimeMaterial.SetInt("_TrailLen", trail); runtimeMaterial.SetFloat("_LineWidth", lineWidth);
        runtimeMaterial.SetFloat("_StreamlineAlpha", streamlineAlpha); runtimeMaterial.SetFloat("_HeadBrightness", headBrightness);
        float unit = Mathf.Max(.000001f, colorSpeedUnitMultiplier), min = Mathf.Max(0, minColorSpeed);
        runtimeMaterial.SetFloat("_MinSpeed", min);
        runtimeMaterial.SetFloat("_MaxSpeed", Mathf.Max(min + .001f, maxColorSpeed > min ? maxColorSpeed : fieldData.MaxSpeed * unit));
        runtimeMaterial.SetFloat("_ColorSpeedUnitMultiplier", unit);
        runtimeMaterial.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Triangles, heads.Length * trail * 6);
    }
    void Release()
    {
        ++generation; building?.Cancel(); building = null; Ready = false;
        pointBuffer?.Release(); headBuffer?.Release(); pointBuffer = headBuffer = null;
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
        runtimeMaterial = null; heads = null; localSpeeds = null;
    }
}
