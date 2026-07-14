using UnityEngine;
using UnityEngine.Timeline;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GraciaSplatsFilePathAttribute : PropertyAttribute
{
    public string Title { get; private set; }
    public string Directory { get; private set; }
    public string Extension { get; private set; }

    public GraciaSplatsFilePathAttribute(string title = "Select File", string directory = "", string extension = "")
    {
        Title = title;
        Directory = directory;
        Extension = extension;
    }
}

[ExecuteAlways]
public class GraciaSplatsModel : MonoBehaviour, ITimeControl
{
    private int handle = -1;
    private string currentFileName = "";

    private bool isPlaying = true;

    private bool timelineMode = false;

    [GraciaSplatsFilePath("Select File", "", "mint,ply,guf")]
    [SerializeField]
    private string fileName;

    [SerializeField]
    [Range(0.01f, 1.0f)]
    public float playbackSpeed = 1.0f;

#if UNITY_EDITOR
    [HideInInspector]
    public Vector3 minBounds = Vector3.one * -1;

    [HideInInspector]
    public Vector3 maxBounds = Vector3.one;
#endif

    void OnEnable()
    {
        Gracia.SetSplatsEnabled(handle, true);
    }

    void OnDisable()
    {
        Gracia.SetSplatsEnabled(handle, false);
    }

    void Update()
    {
        if (string.IsNullOrEmpty(fileName))
            return;

        if (currentFileName != fileName || handle == -1)
        {
            // On mobile platforms, load from app's Documents folder
            string path = (Application.platform == RuntimePlatform.Android ||
                           Application.platform == RuntimePlatform.IPhonePlayer)
                ? Path.Combine(Application.persistentDataPath, Path.GetFileName(fileName))
                : fileName;
            handle = Gracia.AcquireSplatsHandle(handle, path);
            currentFileName = fileName;
        }

        if (handle == -1)
            return;

        Gracia.SetModelMatrix(handle, GetMatrixWithZFlip());

        if (!timelineMode)
            Gracia.SetSplatsDeltaTime(handle, isPlaying ? Time.deltaTime * playbackSpeed * GetGlobalPlaybackSpeed() : 0.0f);

#if UNITY_EDITOR
        Gracia.GetLocalBounds(handle, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ);
        minBounds = new Vector3(minX, minY, minZ);
        maxBounds = new Vector3(maxX, maxY, maxZ);
#endif
    }

    private float GetGlobalPlaybackSpeed()
    {
#if UNITY_EDITOR
        return EditorPrefs.GetBool("Gracia_MintPlaybackEnabled", true) ? 1.0f : 0.0f;
#else
        return 1.0f;
#endif
    }

    void OnDestroy()
    {
        if (handle != -1)
        {
            Gracia.ReleaseSplatsHandle(handle);
            handle = -1;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 center = (minBounds + maxBounds) * 0.5f;
        Vector3 size = maxBounds - minBounds;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(center, size);
        Gizmos.matrix = oldMatrix;
    }
#endif

    public void SetTime(double time)
    {
        timelineMode = true;
        double dt = time - Gracia.GetVideoTime(handle);
        if (dt > 0 && dt < (1.0f / 30.0f))
            Gracia.SetSplatsDeltaTime(handle, (float)dt);
        else
            Gracia.RewindVideo(handle, (float)time);
    }

    public void OnControlTimeStart()
    {
        isPlaying = true;
    }

    public void OnControlTimeStop()
    {
        isPlaying = false;
    }
    
    private Matrix4x4 GetMatrixWithZFlip()
    {
        Vector3 scale = transform.lossyScale;
        scale.z *= -1f;
        return Matrix4x4.TRS(transform.position, transform.rotation, scale);
    }
}
