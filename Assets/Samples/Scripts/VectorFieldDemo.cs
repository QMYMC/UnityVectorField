using UnityEngine;

/// <summary>Standalone scene bootstrap and optional IMGUI controls.</summary>
public class VectorFieldDemo : MonoBehaviour
{
    public ComputeShader simulation;
    public Material shortMaterial, longMaterial;
    public bool showControls = true;
    public VectorFieldInstance Instance { get; private set; }
    public bool Deep { get; private set; }
    public bool Plane { get; private set; }
    Camera view;

    void Start()
    {
        Instance = CreateInstance("Synthetic helix");
        view = Camera.main;
        Reload(false);
    }
    public VectorFieldInstance CreateInstance(string label)
    {
        var root = new GameObject(label); root.SetActive(false);
        root.transform.SetParent(transform, false);
        var instance = root.AddComponent<VectorFieldInstance>();
        instance.Bind();
        instance.Short.computeShader = simulation; instance.Short.mainMaterial = shortMaterial;
        instance.Long.streamlineMaterial = longMaterial;
        instance.SetPalette(Palette());
        root.SetActive(true);
        return instance;
    }
    static Gradient Palette()
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(new Color(.16f,.32f,.9f),0),
            new GradientColorKey(new Color(.05f,.85f,.85f),.32f),
            new GradientColorKey(new Color(.4f,.95f,.36f),.62f),
            new GradientColorKey(new Color(1,.65f,.15f),1)},
            new[] { new GradientAlphaKey(1,0), new GradientAlphaKey(1,1) });
        return gradient;
    }
    public void Reload(bool deep, bool plane = false)
    {
        Deep = deep; Plane = plane;
        Instance.Load(SyntheticVectorField.Create(deep, plane), VectorFieldMapping.Identity);
        Instance.Short.automaticReferenceSize = new Vector3(40, 12, 40);
        Instance.SetColorRange(0, 5);
        if (view != null)
        {
            Vector3 centre = Instance.Data.SimBounds.center;
            view.transform.position = centre + new Vector3(46, 37, -55);
            view.transform.LookAt(centre);
        }
    }
    void Update()
    {
        if (Instance == null) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) Instance.SetMode(VectorFieldMode.Long);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Instance.SetMode(VectorFieldMode.Short);
        if (Input.GetKeyDown(KeyCode.R)) Reload(Deep, Plane);
    }
    void OnGUI()
    {
        if (!showControls || Instance == null) return;
        GUILayout.BeginArea(new Rect(24,24,285,300), GUI.skin.box);
        GUILayout.Label("VECTOR FIELD / synthetic helix");
        GUILayout.Label("1 Long    2 Short    R Reload");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Long")) Instance.SetMode(VectorFieldMode.Long);
        if (GUILayout.Button("Short")) Instance.SetMode(VectorFieldMode.Short);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Shallow 40 x 12 x 40 m")) Reload(false);
        if (GUILayout.Button("Deep 28 x 24 x 18 m")) Reload(true);
        if (GUILayout.Button("Planar 40 x 40 m")) Reload(false, true);
        GUILayout.Label("Mode: " + Instance.mode + " / " + (Deep ? "deep" : Plane ? "plane" : "shallow"));
        GUILayout.Label("Physical speed: 0 - 5 m/s");
        GUILayout.Label("Display speed: " + Instance.Short.speedMultiplier.ToString("F1") + "x");
        float speed = GUILayout.HorizontalSlider(Instance.Short.speedMultiplier, 0, 4);
        Instance.SetDisplaySpeed(speed);
        GUILayout.Label("Short allocated: " + Instance.Short.ActiveMainParticleCount);
        GUILayout.Label("Long lines: " + Instance.Long.ActiveLineCount);
        if (GUILayout.Button(Instance.visible ? "Stop" : "Show")) { if (Instance.visible) Instance.Stop(); else Instance.Show(); }
        GUILayout.EndArea();
    }
}
