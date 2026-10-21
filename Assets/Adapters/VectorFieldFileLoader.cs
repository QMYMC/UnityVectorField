using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Optional desktop file input, separate from the runtime assembly. Never copies files into the project.</summary>
public sealed class VectorFieldFileLoader : MonoBehaviour
{
    public WindFieldData fieldData;
    public VectorFieldTextFormat format = VectorFieldTextFormat.LocalXYZ;
    public string filePath = "";
    public bool loadOnStart;
    public VectorFieldMapping localMapping = VectorFieldMapping.Identity;
    [Tooltip("Legacy geographic adapter only: local display units per degree / per altitude metre.")]
    public float horizontalScale = 100, verticalScale = .001f;
    CancellationTokenSource pending;
    void Start() { if (loadOnStart && !string.IsNullOrWhiteSpace(filePath)) ReloadWithFile(filePath); }
    public async void ReloadWithFile(string path) { await LoadAsync(path); }
    public async Task<bool> LoadAsync(string path)
    {
        Cancel();
        if (fieldData == null) throw new InvalidOperationException("Assign a data component first.");
        fieldData.Unload();
        var request = new CancellationTokenSource(); pending = request;
        var token = request.Token;
        var selected = format; var map = localMapping;
        float horizontal = horizontalScale, vertical = verticalScale;
        Action cancelRequest = () => request.Cancel();
        fieldData.OnUnloaded += cancelRequest;
        try
        {
            var frame = await Task.Run(() => VectorFieldText.Read(path, selected, horizontal, vertical, token), token);
            token.ThrowIfCancellationRequested();
            if (this == null || fieldData == null) return false;
            fieldData.OnUnloaded -= cancelRequest;
            fieldData.Load(frame.Grid, selected == VectorFieldTextFormat.LocalXYZ ? map : frame.Mapping);
            filePath = path;
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception exception) { if (this != null) Debug.LogException(exception, this); return false; }
        finally
        {
            if (fieldData != null) fieldData.OnUnloaded -= cancelRequest;
            if (pending == request) pending = null;
            request.Dispose();
        }
    }
    void Cancel() { pending?.Cancel(); pending = null; }
    void OnDisable() { Cancel(); }
}
