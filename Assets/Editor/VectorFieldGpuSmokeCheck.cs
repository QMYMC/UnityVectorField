using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Reproducible editor setup and actual Play-mode GPU/render acceptance checks.</summary>
[InitializeOnLoad]
public static class VectorFieldGpuSmokeCheck
{
    const string ScenePath = "Assets/Samples/Scenes/VectorFieldDemo.unity";
    const string Request = "VectorField.ValidationRequested";
    static IEnumerator runner;
    static int assertions;
    static double deadline;
    static bool failed;
    static VectorFieldGpuSmokeCheck()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Request, false))
            { assertions = 0; failed = false; runner = Validate(); EditorApplication.update += Tick; }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Request, false))
            { SessionState.SetBool(Request, false); if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool("VectorField.ValidationFailed", false) ? 1 : 0); }
        };
    }
    [MenuItem("Vector Field/Generate synthetic data and demo scene")]
    public static void Setup()
    {
        Directory.CreateDirectory("Assets/Samples/Scenes"); Directory.CreateDirectory("Assets/Samples/Data");
        SyntheticVectorField.Write("Assets/Samples/Data/shallow.xyz.txt", SyntheticVectorField.Create());
        SyntheticVectorField.Write("Assets/Samples/Data/deep.xyz.txt", SyntheticVectorField.Create(true));
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraRoot = new GameObject("Main Camera"); cameraRoot.tag = "MainCamera";
        var camera = cameraRoot.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.018f,.026f,.045f); camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
        camera.fieldOfView = 45;
        cameraRoot.AddComponent<FreeFlyCamera>().moveSpeed = 15;
        var demo = new GameObject("Public synthetic demonstration").AddComponent<VectorFieldDemo>();
        demo.simulation = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/WindFieldCompute.compute");
        demo.shortMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_FlowParticle.mat");
        demo.longMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StreamlineSkeleton.mat");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("VectorField demo generated from analytic data.");
    }
    [MenuItem("Vector Field/Validate Play mode and capture README images")]
    public static void Run()
    {
        if (!File.Exists(ScenePath)) Setup();
        else EditorSceneManager.OpenScene(ScenePath);
        Directory.CreateDirectory("ValidationOutput"); Directory.CreateDirectory("docs/images");
        SessionState.SetBool(Request, true); SessionState.SetBool("VectorField.ValidationFailed", false);
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        try { if (runner.MoveNext()) return; Finish(); }
        catch (Exception exception) { failed = true; Debug.LogException(exception); Finish(); }
    }
    static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool("VectorField.ValidationFailed", failed);
        File.WriteAllText("ValidationOutput/play-mode.txt", (failed ? "FAIL" : "PASS") + " assertions=" + assertions +
            "\nUnity=" + Application.unityVersion + "\nGPU=" + SystemInfo.graphicsDeviceName + "\nAPI=" + SystemInfo.graphicsDeviceType + "\n");
        Debug.Log("VectorField Play-mode " + (failed ? "FAIL" : "PASS") + ": " + assertions + " assertions.");
        EditorApplication.isPlaying = false;
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); assertions++; Debug.Log("VF CHECK: " + message); }
    static IEnumerator Wait(Func<bool> condition)
    {
        deadline = EditorApplication.timeSinceStartup + 45;
        while (!condition())
        { if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Field did not become ready."); yield return null; }
    }
    static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    static IEnumerator Validate()
    {
        var demo = UnityEngine.Object.FindObjectOfType<VectorFieldDemo>();
        yield return null;
        while (demo.Instance == null) yield return null;
        var a = demo.Instance;
        var wait = Wait(() => a.Long.Ready); while (wait.MoveNext()) yield return null;
        Check(a.Long.ActiveLineCount > 10, "Shallow Long paths");
        var frames = Frames(45); while (frames.MoveNext()) yield return null;
        Capture("docs/images/long-mode.png");
        a.SetMode(VectorFieldMode.Short);
        wait = Wait(() => a.Short.Ready); while (wait.MoveNext()) yield return null;
        Check(a.Short.ActiveMainParticleCount > 0 && !a.Long.Ready, "Switch Long to Short releases Long resources");
        frames = Frames(90); while (frames.MoveNext()) yield return null;
        Capture("docs/images/short-mode.png");
        a.SetDisplaySpeed(0);
        frames = Frames(3); while (frames.MoveNext()) yield return null;
        var particles = a.Short.ReadParticles(); int alive = 0;
        foreach (var p in particles)
        {
            if (p.life <= 0) continue;
            Check(a.Data.TrySampleLocal(p.position, out var v), "GPU position remains in a valid cell");
            Check(Mathf.Abs(v.magnitude - p.physicalSpeed) < .002f, "GPU and CPU physical speeds agree at display speed zero");
            alive++;
            if (alive == 16) break;
        }
        Check(alive == 16, "At least 16 live GPU samples");
        var local = particles[0].position;
        a.transform.SetPositionAndRotation(new Vector3(3,5,7), Quaternion.Euler(20,40,10));
        a.transform.localScale = new Vector3(1.2f,.8f,1.4f);
        var world = a.transform.TransformPoint(local);
        Check((a.Data.SampleWorldVelocity(world) - a.transform.TransformVector(a.Data.Frame.SampleWorldVelocity(local))).magnitude < .001f,
            "Translation/rotation/nonuniform scale preserve velocity mapping");
        Capture("ValidationOutput/transformed-short.png");
        a.SetMode(VectorFieldMode.Long);
        wait = Wait(() => a.Long.Ready); while (wait.MoveNext()) yield return null;
        Capture("ValidationOutput/transformed-long.png");
        a.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); a.transform.localScale = Vector3.one;
        demo.Reload(true); a.SetDisplaySpeed(1);
        wait = Wait(() => a.Long.Ready); while (wait.MoveNext()) yield return null;
        Check(a.Long.ActiveLineCount > 0, "Deep Long paths"); Capture("ValidationOutput/deep-long.png");
        a.SetMode(VectorFieldMode.Short);
        wait = Wait(() => a.Short.Ready); while (wait.MoveNext()) yield return null;
        Check(a.Short.ActiveMainParticleCount > 0, "Deep Short particles"); Capture("ValidationOutput/deep-short.png");
        int firstCount = a.Short.ActiveMainParticleCount;
        var b = demo.CreateInstance("Independent instance"); b.SetMode(VectorFieldMode.Short);
        b.Short.automaticParticleCount = false; b.Short.mainParticleCount = 17; b.Short.fixedParticleCountUnit = 3;
        var red = new Gradient(); red.SetKeys(new[] {new GradientColorKey(Color.red,0),new GradientColorKey(Color.red,1)},
            new[] {new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
        b.SetPalette(red); b.SetDisplaySpeed(.25f); b.Load(SyntheticVectorField.Create(), VectorFieldMapping.Identity);
        wait = Wait(() => b.Short.Ready); while (wait.MoveNext()) yield return null;
        Check(b.Short.ActiveMainParticleCount == 51, "Fixed count uses configurable unit");
        Check(a.Short.ActiveMainParticleCount == firstCount, "Second allocation does not change first instance count");
        Check(a.Short.speedMultiplier == 1 && b.Short.speedMultiplier == .25f, "Instance speeds isolated");
        Check(a.Palette.RampTexture != b.Palette.RampTexture && a.Palette.RampTexture.GetPixel(0,0) != Color.red, "Palettes isolated");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Check(typeof(WindParticleSystem).GetField("runtimeCompute",flags).GetValue(a.Short) != typeof(WindParticleSystem).GetField("runtimeCompute",flags).GetValue(b.Short), "Compute shaders isolated");
        Check(typeof(WindParticleSystem).GetField("runtimeMaterial",flags).GetValue(a.Short) != typeof(WindParticleSystem).GetField("runtimeMaterial",flags).GetValue(b.Short), "Materials isolated");
        var xs = new[] {-4f,1f,4f}; var ys = new[] {-6f,-1f,3f}; var zs = new[] {-4f,4f};
        var vectors = new Vector3[18];
        for (int z=0;z<2;z++) for(int y=0;y<3;y++) for(int x=0;x<3;x++)
            vectors[x+3*(y+3*z)] = new Vector3(2+xs[x]*.1f,1+ys[y]*.1f,.3f);
        var mapping = VectorFieldMapping.Identity;
        mapping.origin = new Vector3(-2,-4,5); mapping.positionUnitsToLocal = new Vector3(2,.5f,1.2f);
        b.SetDisplaySpeed(0); b.Load(new VectorFieldGrid(xs,ys,zs,vectors),mapping);
        wait = Wait(() => b.Short.Ready); while(wait.MoveNext()) yield return null;
        int probes = 0;
        foreach(var p in b.Short.ReadParticles()) if(p.life>0)
        {
            Check(b.Data.TrySampleLocal(p.position,out var physical) && Mathf.Abs(physical.magnitude-p.physicalSpeed)<.002f,
                "Nonuniform GPU sampling with nonzero origin and unit mapping");
            if(++probes>=8) break;
        }
        Check(probes==8,"Mapped nonuniform GPU probes present");
        var planeGrid = new VectorFieldGrid(new[]{-4f,4f},new[]{3f},new[]{-4f,4f},
            new[]{new Vector3(1,4,.3f),new Vector3(1,4,.3f),new Vector3(1,4,.3f),new Vector3(1,4,.3f)});
        b.Load(planeGrid,VectorFieldMapping.Identity);
        wait = Wait(() => b.Short.Ready); while(wait.MoveNext()) yield return null;
        var planeProbe = b.Short.ReadParticles()[0];
        Check(Mathf.Abs(planeProbe.position.y-3)<1e-5f && Mathf.Abs(planeProbe.velocity.y)<1e-5f &&
            Mathf.Abs(planeProbe.physicalSpeed-new Vector3(1,4,.3f).magnitude)<.002f,
            "Planar GPU retains raw vertical speed for colour while constraining motion");
        b.SetMode(VectorFieldMode.Long);
        wait = Wait(() => b.Long.Ready); while(wait.MoveNext()) yield return null;
        Check(b.Long.ActiveLineCount>0,"Long handles planar vectors with vertical components");
        var loader = b.gameObject.AddComponent<VectorFieldFileLoader>(); loader.fieldData=b.Data;
        var firstLoad = loader.LoadAsync("Assets/Samples/Data/deep.xyz.txt");
        var secondLoad = loader.LoadAsync("Assets/Samples/Data/shallow.xyz.txt");
        wait = Wait(() => firstLoad.IsCompleted && secondLoad.IsCompleted); while(wait.MoveNext()) yield return null;
        Check(!firstLoad.Result && secondLoad.Result && b.Data.Frame.Grid.SizeY==7,"File replacement cancels old parse and loads newest file");
        var pendingLoad = loader.LoadAsync("Assets/Samples/Data/deep.xyz.txt"); b.Unload();
        wait = Wait(() => pendingLoad.IsCompleted); while(wait.MoveNext()) yield return null;
        Check(!pendingLoad.Result && !b.Data.IsLoaded && !b.Short.Ready && !b.Long.Ready,"Unload cancels pending file input and all render resources");
        UnityEngine.Object.Destroy(b.gameObject);
        demo.Reload(false, true);
        wait = Wait(() => a.Short.Ready); while (wait.MoveNext()) yield return null;
        int planeSamples = 0;
        foreach (var p in a.Short.ReadParticles()) if (p.life > 0)
        { Check(Mathf.Abs(p.position.y) < .0001f, "Planar GPU constraint"); if (++planeSamples >= 16) break; }
        a.SetMode(VectorFieldMode.Long);
        wait = Wait(() => a.Long.Ready); while (wait.MoveNext()) yield return null;
        Check(a.Long.ActiveLineCount > 0, "Planar Long");
        demo.Reload(true); demo.Reload(false); a.Unload();
        frames = Frames(10); while (frames.MoveNext()) yield return null;
        Check(!a.Long.Ready && !a.Short.Ready && !a.Data.IsLoaded, "Rapid replacement then unload cancels stale work");
        UnityEngine.SceneManagement.SceneManager.LoadScene("VectorFieldDemo");
        yield return null;
        demo = UnityEngine.Object.FindObjectOfType<VectorFieldDemo>();
        wait = Wait(() => demo != null && demo.Instance != null && demo.Instance.Long.Ready); while (wait.MoveNext()) yield return null;
        Check(demo.Instance.Long.ActiveLineCount > 0, "Scene restart rebuilds Long");
        demo.Instance.SetMode(VectorFieldMode.Short);
        wait = Wait(() => demo.Instance.Short.Ready); while (wait.MoveNext()) yield return null;
        Check(demo.Instance.Short.ActiveMainParticleCount > 0, "Scene restart rebuilds Short");
    }
    static void Capture(string path)
    {
        var camera = Camera.main;
        var target = new RenderTexture(1440,900,24);
        var texture = new Texture2D(1440,900,TextureFormat.RGB24,false);
        var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0,0,1440,900),0,0); texture.Apply();
            int coloured = 0;
            foreach (var p in texture.GetPixels32()) if (Math.Max(p.r,Math.Max(p.g,p.b)) > 50) coloured++;
            Check(coloured > 200, "Nonempty runtime render: " + path + " (pixels=" + coloured + ")");
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        { camera.targetTexture = previousTarget; RenderTexture.active = previous; UnityEngine.Object.Destroy(texture); target.Release(); UnityEngine.Object.Destroy(target); }
    }
}
