using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Short particles: local simulation, physical-speed colouring and per-instance GPU ownership.</summary>
public class WindParticleSystem : MonoBehaviour
{
    public WindFieldData fieldData;
    public WindColorRamp colorRamp;
    public ComputeShader computeShader;
    public Material mainMaterial;
    [Header("Physical colour range (after explicit colour unit conversion)")]
    public float minColorSpeed;
    public float maxColorSpeed;
    [Min(.000001f)] public float colorSpeedUnitMultiplier = 1;
    [Header("Allocation in local display units; maximum applies to both policies")]
    public bool automaticParticleCount = true;
    [Min(0)] public int mainParticleCount = 10000;
    [Min(1)] public int fixedParticleCountUnit = 1;
    public Vector3 automaticReferenceSize = new Vector3(40, 12, 40);
    [Min(0)] public int automaticReferenceParticleCount = 10000;
    [Min(0)] public int automaticParticleLimit = 100000;
    [Header("Appearance in local display units")]
    [Min(.001f)] public float particleScale = .12f;
    public float baseAspect = 2;
    public float stretchMult = .4f;
    public float coreBrightness = .3f;
    public float nearFadeDistance = .5f;
    [Min(0)] public float minVisibleSpeed;
    [Range(0, 1)] public float altFogStrength;
    [Range(0, 1)] public float opacity = .9f;
    [Header("Simulation")]
    [Min(.01f)] public float particleLifeTime = 8;
    [Min(0)] public float speedMultiplier = 1;
    [Range(0, .99f)] public float inertia;

    public int ActiveMainParticleCount { get; private set; }
    public double EstimatedVisibleMeasure { get; private set; }
    public bool Ready => ready;
    ComputeShader runtimeCompute;
    Material runtimeMaterial;
    ComputeBuffer particles, args, xAxis, yAxis, zAxis, spawn;
    CancellationTokenSource building;
    int generation, initKernel, updateKernel, configHash;
    bool ready;

    void OnEnable()
    {
        if (fieldData == null) return;
        fieldData.OnLoaded += Rebuild;
        fieldData.OnUnloaded += Release;
        Rebuild();
    }
    void OnDisable()
    {
        if (fieldData != null) { fieldData.OnLoaded -= Rebuild; fieldData.OnUnloaded -= Release; }
        Release();
    }
    public void Rebuild() { if (isActiveAndEnabled) _ = BuildAsync(); }
    int ConfigurationHash()
    {
        unchecked
        {
            int hash = mainParticleCount;
            hash = hash * 31 + fixedParticleCountUnit;
            hash = hash * 31 + automaticParticleCount.GetHashCode();
            hash = hash * 31 + automaticReferenceSize.GetHashCode();
            hash = hash * 31 + automaticReferenceParticleCount;
            hash = hash * 31 + automaticParticleLimit;
            hash = hash * 31 + minVisibleSpeed.GetHashCode();
            hash = hash * 31 + minColorSpeed.GetHashCode();
            hash = hash * 31 + maxColorSpeed.GetHashCode();
            hash = hash * 31 + colorSpeedUnitMultiplier.GetHashCode();
            return hash * 31 + (colorRamp == null ? 0 : colorRamp.Revision);
        }
    }
    async Task BuildAsync()
    {
        Release();
        if (fieldData == null || !fieldData.IsLoaded || colorRamp == null || computeShader == null || mainMaterial == null) return;
        if (!SystemInfo.supportsComputeShaders || SystemInfo.graphicsShaderLevel < 45)
        { Debug.LogError("Short requires Compute Shader and Shader Model 4.5.", this); return; }
        var request = new CancellationTokenSource();
        building = request;
        var token = request.Token;
        int version = generation;
        var frame = fieldData.Frame;
        ResolveColorRange(out float min, out float max);
        Color[] ramp = colorRamp.RampTexture.GetPixels();
        float unit = Mathf.Max(.000001f, colorSpeedUnitMultiplier), threshold = Mathf.Max(0, minVisibleSpeed);
        configHash = ConfigurationHash();
        try
        {
            var table = await Task.Run(() => VectorFieldSpawnTable.Build(frame, ramp, min, max, unit, threshold, token), token);
            token.ThrowIfCancellationRequested();
            if (version != generation || !isActiveAndEnabled || fieldData.Frame != frame) return;
            EstimatedVisibleMeasure = table.VisibleMeasure;
            int count = table.Cells.Length == 0 ? 0 : VectorFieldSpawnTable.ResolveCount(automaticParticleCount,
                mainParticleCount, fixedParticleCountUnit, table.VisibleMeasure, frame.Grid.Is2D,
                automaticReferenceSize, automaticReferenceParticleCount, automaticParticleLimit);
            ActiveMainParticleCount = count;
            if (count == 0) return;
            runtimeCompute = Instantiate(computeShader);
            runtimeMaterial = Instantiate(mainMaterial);
            initKernel = runtimeCompute.FindKernel("InitParticles");
            updateKernel = runtimeCompute.FindKernel("UpdateParticles");
            particles = new ComputeBuffer(count, 40);
            // GPU initialization reads only seed and overwrites every other field.
            particles.SetData(new WindParticle[count]);
            args = new ComputeBuffer(1, 16, ComputeBufferType.IndirectArguments);
            args.SetData(new uint[] { 6, (uint)count, 0, 0 });
            xAxis = Axis(frame.Grid.CopyAxis(0)); yAxis = Axis(frame.Grid.CopyAxis(1)); zAxis = Axis(frame.Grid.CopyAxis(2));
            spawn = new ComputeBuffer(table.Cells.Length, 32); spawn.SetData(table.Cells);
            runtimeCompute.SetInts("_GridSize", frame.Grid.SizeX, frame.Grid.SizeY, frame.Grid.SizeZ);
            runtimeCompute.SetInt("_ParticleCount", count);
            runtimeCompute.SetInt("_SpawnCount", table.Cells.Length);
            runtimeCompute.SetFloat("_CandidateMeasure", table.CandidateMeasure);
            runtimeCompute.SetVector("_DataOrigin", frame.Mapping.origin);
            runtimeCompute.SetVector("_PositionScale", frame.Mapping.positionUnitsToLocal);
            runtimeCompute.SetFloat("_VelocityConversion", frame.Mapping.velocityUnitsToPositionUnits);
            foreach (int kernel in new[] { initKernel, updateKernel })
            {
                runtimeCompute.SetBuffer(kernel, "particles", particles);
                runtimeCompute.SetBuffer(kernel, "_XAxis", xAxis); runtimeCompute.SetBuffer(kernel, "_YAxis", yAxis);
                runtimeCompute.SetBuffer(kernel, "_ZAxis", zAxis); runtimeCompute.SetBuffer(kernel, "_SpawnCells", spawn);
                runtimeCompute.SetTexture(kernel, "_FlowField", fieldData.FlowTexture);
                runtimeCompute.SetTexture(kernel, "_ColorRamp", colorRamp.RampTexture);
            }
            SetSimulation();
            runtimeCompute.Dispatch(initKernel, ParticleDispatchMath.ThreadGroupCount(count, 64), 1, 1);
            ready = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { if (this != null) { Release(); Debug.LogException(exception, this); } }
        finally { if (building == request) building = null; request.Dispose(); }
    }
    static ComputeBuffer Axis(float[] values) { var buffer = new ComputeBuffer(values.Length, 4); buffer.SetData(values); return buffer; }
    void SetSimulation()
    {
        ResolveColorRange(out float min, out float max);
        runtimeCompute.SetFloat("_MinSpeed", min); runtimeCompute.SetFloat("_MaxSpeed", max);
        runtimeCompute.SetFloat("_ColorUnit", Mathf.Max(.000001f, colorSpeedUnitMultiplier));
        runtimeCompute.SetFloat("_MinVisibleSpeed", Mathf.Max(0, minVisibleSpeed));
        runtimeCompute.SetFloat("_DeltaTime", Mathf.Min(Time.deltaTime, .1f));
        runtimeCompute.SetFloat("_SpeedMultiplier", Mathf.Max(0, speedMultiplier));
        runtimeCompute.SetFloat("_ParticleLifeTime", Mathf.Max(.01f, particleLifeTime));
        runtimeCompute.SetFloat("_Inertia", Mathf.Clamp(inertia, 0, .99f));
    }
    void Update()
    {
        if (fieldData == null || !fieldData.IsLoaded) return;
        if (configHash != ConfigurationHash()) { Rebuild(); return; }
        if (!ready) return;
        SetSimulation();
        runtimeCompute.Dispatch(updateKernel, ParticleDispatchMath.ThreadGroupCount(ActiveMainParticleCount, 64), 1, 1);
    }
    void OnRenderObject()
    {
        if (!isActiveAndEnabled || !ready || fieldData == null || !fieldData.IsLoaded) return;
        ResolveColorRange(out float min, out float max);
        runtimeMaterial.SetBuffer("particles", particles);
        runtimeMaterial.SetTexture("_ColorRamp", colorRamp.RampTexture);
        runtimeMaterial.SetMatrix("_FieldLocalToWorld", fieldData.transform.localToWorldMatrix);
        runtimeMaterial.SetMatrix("_FieldWorldToLocal", fieldData.transform.worldToLocalMatrix);
        runtimeMaterial.SetFloat("_MinSpeed", min); runtimeMaterial.SetFloat("_MaxSpeed", max);
        runtimeMaterial.SetFloat("_ColorSpeedUnitMultiplier", Mathf.Max(.000001f, colorSpeedUnitMultiplier));
        runtimeMaterial.SetFloat("_Opacity", opacity);
        runtimeMaterial.SetFloat("_Scale", particleScale); runtimeMaterial.SetFloat("_BaseAspect", baseAspect);
        runtimeMaterial.SetFloat("_StretchMult", stretchMult); runtimeMaterial.SetFloat("_CoreBrightness", coreBrightness);
        runtimeMaterial.SetFloat("_NearFadeDistance", nearFadeDistance); runtimeMaterial.SetFloat("_MinVisibleSpeed", minVisibleSpeed);
        runtimeMaterial.SetFloat("_AltFogStrength", altFogStrength);
        runtimeMaterial.SetVector("_BoundMin", fieldData.SimBounds.min); runtimeMaterial.SetVector("_BoundSize", fieldData.SimBounds.size);
        runtimeMaterial.SetPass(0);
        Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, args);
    }
    void ResolveColorRange(out float min, out float max)
    {
        min = Mathf.Max(0, minColorSpeed);
        max = Mathf.Max(min + .001f, maxColorSpeed > min ? maxColorSpeed : fieldData.MaxSpeed * Mathf.Max(.000001f, colorSpeedUnitMultiplier));
    }
    void Release()
    {
        ++generation; building?.Cancel(); building = null; ready = false; ActiveMainParticleCount = 0;
        particles?.Release(); args?.Release(); xAxis?.Release(); yAxis?.Release(); zAxis?.Release(); spawn?.Release();
        particles = args = xAxis = yAxis = zAxis = spawn = null;
        if (runtimeCompute != null) Destroy(runtimeCompute);
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
        runtimeCompute = null; runtimeMaterial = null;
    }
    public WindParticle[] ReadParticles()
    { var result = new WindParticle[ActiveMainParticleCount]; if (ready) particles.GetData(result); return result; }
}
