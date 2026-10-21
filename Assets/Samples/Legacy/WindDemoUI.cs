using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Demo UI — 切换 StreamlineSystem (Long) / WindParticleSystem (Short)，
/// 并提供 Windows 文件选择对话框切换风场数据文件。
/// </summary>
public class WindDemoUI : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("comdlg32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    static extern bool GetOpenFileName(ref OpenFileName ofn);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    struct OpenFileName
    {
        public int      lStructSize;
        public System.IntPtr hwndOwner;
        public System.IntPtr hInstance;
        public string   lpstrFilter;
        public string   lpstrCustomFilter;
        public int      nMaxCustFilter;
        public int      nFilterIndex;
        public string   lpstrFile;
        public int      nMaxFile;
        public string   lpstrFileTitle;
        public int      nMaxFileTitle;
        public string   lpstrInitialDir;
        public string   lpstrTitle;
        public int      Flags;
        public short    nFileOffset;
        public short    nFileExtension;
        public string   lpstrDefExt;
        public System.IntPtr lCustData;
        public System.IntPtr lpfnHook;
        public string   lpTemplateName;
        public System.IntPtr pvReserved;
        public int      dwReserved;
        public int      FlagsEx;
    }
#endif
    enum WindMode { Streamline, Particle }

    [Header("引用")]
    public WindFieldData      fieldData;
    public StreamlineSystem   streamlineSystem;
    public WindParticleSystem windParticleSystem;
    VectorFieldFileLoader fileLoader;

    [Header("UI 位置")]
    public int uiX = 10;
    public int uiY = 10;
    public int uiWidth = 320;

    WindMode  _currentMode = WindMode.Streamline;
    string    _status      = "";
    float     _statusTimer;
    bool      _showManualInput;
    string    _manualPath = "";

    void Start()
    {
        if (fieldData != null)
        {
            fileLoader = fieldData.GetComponent<VectorFieldFileLoader>();
            if (fileLoader == null) fileLoader = fieldData.gameObject.AddComponent<VectorFieldFileLoader>();
            fileLoader.fieldData = fieldData;
            fileLoader.format = VectorFieldTextFormat.LongitudeLatitudeAltitude;
        }
        if (streamlineSystem == null || windParticleSystem == null || fieldData == null)
        {
            Debug.LogError("[WindDemoUI] 请在 Inspector 中赋值所有引用。");
            return;
        }

        SwitchMode(WindMode.Streamline);
    }

    void Update()
    {
        if (_statusTimer > 0f)
        {
            _statusTimer -= Time.deltaTime;
            if (_statusTimer <= 0f)
                _status = "";
        }
    }

    void OnGUI()
    {
        if (streamlineSystem == null || windParticleSystem == null || fieldData == null)
        {
            GUILayout.BeginArea(new Rect(uiX, uiY, uiWidth, 80));
            GUILayout.Label("[WindDemoUI] 引用未配置", GUI.skin.box);
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(uiX, uiY, uiWidth, 300));

        // ── 标题 ──
        GUILayout.Label("══ Wind Field Demo ══", GUI.skin.box);

        // ── 模式切换 ──
        GUILayout.Label("可视化模式");
        string modeLabel   = _currentMode == WindMode.Streamline ? "Streamline (Long)" : "Particle (Short)";
        string toggleLabel = _currentMode == WindMode.Streamline
            ? "→ 切换到 Particle (Short)"
            : "→ 切换到 Streamline (Long)";

        GUI.backgroundColor = _currentMode == WindMode.Streamline
            ? new Color(0.3f, 0.5f, 1f) : new Color(1f, 0.6f, 0.2f);
        GUILayout.Label($"当前: {modeLabel}");

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button(toggleLabel, GUILayout.Height(30)))
        {
            WindMode next = _currentMode == WindMode.Streamline
                ? WindMode.Particle : WindMode.Streamline;
            SwitchMode(next);
        }

        GUILayout.Space(8);

        // ── 数据文件选择 ──
        GUILayout.Label("风场数据文件");
        GUILayout.Label($"当前: {Path.GetFileName(fileLoader.filePath)}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("选择数据文件...", GUILayout.Height(30)))
            OpenFileDialog();

        if (GUILayout.Button("手动输入", GUILayout.Height(30), GUILayout.Width(80)))
            _showManualInput = !_showManualInput;
        GUILayout.EndHorizontal();

        if (_showManualInput)
        {
            GUILayout.BeginHorizontal();
            _manualPath = GUILayout.TextField(_manualPath);
            if (GUILayout.Button("加载", GUILayout.Width(50)))
            {
                if (!string.IsNullOrEmpty(_manualPath))
                {
                    fileLoader.ReloadWithFile(_manualPath);
                    SetStatus($"加载中: {Path.GetFileName(_manualPath)}");
                    _showManualInput = false;
                    _manualPath      = "";
                }
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);

        // ── 退出程序 ──
        if (GUILayout.Button("退出程序", GUILayout.Height(30)))
        {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
            Application.Quit();
    #endif
        }

        GUILayout.Space(8);

        // ── 状态信息 ──
        if (!string.IsNullOrEmpty(_status))
        {
            GUILayout.Label($"状态: {_status}");
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndArea();
    }

    void SwitchMode(WindMode mode)
    {
        _currentMode = mode;
        bool useStreamline = mode == WindMode.Streamline;

        streamlineSystem.gameObject.SetActive(useStreamline);
        windParticleSystem.gameObject.SetActive(!useStreamline);

        SetStatus($"已切换到 {mode} 模式");
    }

    void OpenFileDialog()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel(
            "选择风场数据文件",
            Application.streamingAssetsPath,
            "txt");
        if (!string.IsNullOrEmpty(path))
        {
            fileLoader.ReloadWithFile(path);
            SetStatus($"加载中: {Path.GetFileName(path)}");
        }

#elif UNITY_STANDALONE_WIN
        var ofn = new OpenFileName();
        ofn.lStructSize    = System.Runtime.InteropServices.Marshal.SizeOf(ofn);
        ofn.lpstrFilter    = "文本文件\0*.txt\0所有文件\0*.*\0";
        ofn.lpstrFile      = new string('\0', 512);
        ofn.nMaxFile       = 512;
        ofn.lpstrInitialDir = Application.streamingAssetsPath;
        ofn.lpstrTitle     = "选择风场数据文件";
        ofn.Flags          = 0x00080000 | 0x00001000; // OFN_EXPLORER | OFN_FILEMUSTEXIST

        if (GetOpenFileName(ref ofn))
        {
            string fpath = ofn.lpstrFile.TrimEnd('\0');
            if (!string.IsNullOrEmpty(fpath))
            {
                fileLoader.ReloadWithFile(fpath);
                SetStatus($"加载中: {Path.GetFileName(fpath)}");
            }
        }

#else
        // macOS / Linux / WebGL — 没有原生文件对话框，回退到手动输入
        _showManualInput = true;
        SetStatus("当前平台不支持文件对话框，请使用手动输入");
#endif
    }

    void SetStatus(string msg)
    {
        _status      = msg;
        _statusTimer = 5f;
        Debug.Log($"[WindDemoUI] {msg}");
    }
}
