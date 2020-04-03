/*
Copyright 2017 Google Inc. All Rights Reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;

public enum CubeFaceResolution
{
k512 = 512,
k1024 = 1024,
k1536 = 1536,
k2048 = 2048,
k4096 = 4096,
k8192 = 8192
}

public enum PositionSampleCount {
k2 = 2,
k4 = 4,
k8 = 8,
k16 = 16,
k32 = 32,
k64 = 64,
k128 = 128,
k256 = 256,
}

public enum CaptureDynamicRange {
// Standard (or low) dynamic range, e.g. sRGB.
kSDR = 0,
// High dynamic range with medium precision floating point data; requires half float render targets.
kHDR16 = 1,
// High dynamic range with full float precision render targets.
kHDR = 2,
}

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class CaptureHeadbox : MonoBehaviour {
    
    // Capture Settings --
    [Tooltip("The dimensions of the headbox.")]
    public Vector3 size_ = Vector3.one;
    [Tooltip("The number of samples per face of the headbox.")]
    public PositionSampleCount samples_per_face_ = PositionSampleCount.k32;
    [Tooltip("The resolution of the center image, taken at the camera position at the center of the headbox. This should be 4x higher than the resolution of the remaining samples, for antialiasing.")]
    public CubeFaceResolution center_resolution_ = CubeFaceResolution.k4096;
    [Tooltip("The resolution of all samples other than the center.")]
    public CubeFaceResolution resolution_ = CubeFaceResolution.k1024;
    [Tooltip("Capture in standard (SDR) or high dynamic range (HDR). HDR requires floating-point render targets, the Camera Component have allow HDR enabled, and enables EXR output.")]
    public CaptureDynamicRange dynamic_range_ = CaptureDynamicRange.kSDR;
    [Tooltip("Root destination folder for capture data; empty instructs the capture to use an automatically-generated, unique folder in the project temp folder.")]
    public string output_folder_ = "";
    // Indicates location of most-recent capture artifacts.
    public string last_output_dir_;

    // Pipeline Settings
    [Tooltip("Executable for the Seurat pipeline executable")]
    public string seurat_exec_ = "";
    [Tooltip("Destination folder for Seurat output mesh")]
    public string seurat_output_folder_ = "";
    [Tooltip("Seurat Output Name")]
    public string seurat_output_name_ = "";
    [Tooltip("If true, store output geometry in a cache. Speeds up repeated processes, good for iterating on texture settings.")]
    public bool use_cache_ = false;
    [Tooltip("Folder to store geometry artifacts in")]
    public string cache_folder_;
    [Tooltip("Seurat Commandline Params")]
    public SeuratParams options = new SeuratParams
    {
        //Initialize with default values - note that premultiply_alphas is by default true, but Unity expects false
        premultiply_alphas = false,
        gamma = 1.0f,
        triangle_count = 72000,
        skybox_radius = 200.0f,
        fast_preview = false
    };

    // Import Settings
    [Tooltip("Indicates where to copy seurat mesh & texture to")]
    public string asset_path_;
    [Tooltip("Indicates currently imported object")]
    public GameObject current_obj_;
    [Tooltip("Indicates currently imported texture")]
    public Texture2D current_tex_;

    // Scene Builder Settings
    [Tooltip("Prefab to be resized for the headbox")]
    public GameObject headbox_prefab_;
    [Tooltip("Shader to use for each seurat material")]
    public Shader seurat_shader_;
    [Tooltip("Relative path to place each seurat mesh in, relative to the headbox prefab. Leave blank to spawn at root")]
    public string prefab_path_;

    private Camera color_camera_;
    private CaptureBuilder capture_;

    public Camera ColorCamera {
        get {
            if (color_camera_ == null) {
                color_camera_ = GetComponent<Camera>();
            }
            return color_camera_;
        }
    }

#if UNITY_EDITOR
    private string target_model_path;
    private string target_texture_path;

    public bool FilesExistInProject
    {
        get
        {
            if (string.IsNullOrEmpty(target_model_path) && string.IsNullOrEmpty(target_texture_path))
                return false;
            return File.Exists(Application.dataPath + target_model_path) && File.Exists(Application.dataPath + target_texture_path);
        }
    }

    public string GetArgString()
    {
        string input = Path.Combine(output_folder_, "manifest.json");
        string output = Path.Combine(seurat_output_folder_, seurat_output_name_);
        return "-input_path=" + input + " -output_path=" + (use_cache_ ? output + " -cache_path=" + cache_folder_ + options.GetArgs() : options.GetArgs());
    }

    public void CopyFiles()
    {
        string model_name = seurat_output_name_ + ".obj";
        string tex_name = seurat_output_name_ + ".png";
        string model_path = Path.Combine(seurat_output_folder_, model_name);
        string png_path = Path.Combine(seurat_output_folder_, tex_name);
        target_model_path = Path.Combine(asset_path_, model_name);
        target_texture_path = Path.Combine(asset_path_, tex_name);

        FileUtil.ReplaceFile(model_path, Application.dataPath + target_model_path);
        FileUtil.ReplaceFile(png_path, Application.dataPath + target_texture_path);
    }

    public void ImportSeurat()
    { 
        string target_model_path = Path.Combine(asset_path_, seurat_output_name_ + ".obj");
        string target_texture_path = Path.Combine(asset_path_, seurat_output_name_ + ".png");
        
        AssetDatabase.ImportAsset("Assets" + target_texture_path);
        AssetDatabase.ImportAsset("Assets" + target_model_path);
        CorrectTextureSettings();

    }

    public void FetchAssets()
    {
        string target_model_path = Path.Combine(asset_path_, seurat_output_name_ + ".obj");
        string target_texture_path = Path.Combine(asset_path_, seurat_output_name_ + ".png");
        current_tex_ = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets" + target_texture_path, typeof(Texture2D));
        current_obj_ = (GameObject)AssetDatabase.LoadAssetAtPath("Assets" + target_model_path, typeof(GameObject));
    }

    public void CorrectTextureSettings()
    {
        string target_texture_path = Path.Combine(asset_path_, seurat_output_name_ + ".png");
        TextureImporter textureImporter = AssetImporter.GetAtPath("Assets" + target_texture_path) as TextureImporter;
        textureImporter.mipmapEnabled = false;
        textureImporter.wrapMode = TextureWrapMode.Clamp;
        textureImporter.filterMode = FilterMode.Bilinear;
        textureImporter.maxTextureSize = 4096;
        EditorUtility.SetDirty(textureImporter);
        textureImporter.SaveAndReimport();
    }

    void Update() {
        if (IsCapturing()) {
            RunCapture();
        }

        if (Input.GetKeyDown(KeyCode.BackQuote)) {
            ToggleCaptureMode();
        }
    }

    bool IsCapturing() {
        return capture_ != null;
    }

    void RunCapture() {
        Debug.Log("Capturing headbox samples...", this);
        capture_.CaptureAllHeadboxSamples();
        if (capture_.IsCaptureComplete()) {
            StopCapture();
        }
    }

    void ToggleCaptureMode() {
        if (IsCapturing()) {
            StopCapture();
        } else {
            StartCapture();
        }
    }

    void StartCapture() {
        Debug.Log("Capture start - temporarily setting fixed framerate.", this);
        capture_ = new CaptureBuilder();

        string capture_output_folder = output_folder_;
        if (capture_output_folder.Length <= 0) {
            capture_output_folder = FileUtil.GetUniqueTempPathInProject();
        }
        Directory.CreateDirectory(capture_output_folder);
        capture_.BeginCapture(this, capture_output_folder, 1, new CaptureStatus());

        // See Time.CaptureFramerate example, e.g. here:
        // https://docs.unity3d.com/ScriptReference/Time-captureFramerate.html
        Time.captureFramerate = 60;
    }

    void StopCapture() {
        Debug.Log("Capture stop", this);
        if (capture_ != null) {
            capture_.EndCapture();
        }
        capture_ = null;
        Time.captureFramerate = 0;
    }

#endif

    public void BuildCapture(bool removeCaptureAfter = false, bool setActiveAfter = true)
    {
        if (current_obj_ == null || current_tex_ == null || seurat_shader_ == null)
            return;

        // Setup material
        Material newMat = new Material(seurat_shader_);
        newMat.SetTexture("_MainTex", current_tex_);

        GameObject originalParent;
        GameObject parent;
        GameObject seuratMesh;
        if(headbox_prefab_ != null)
        {
            parent = Instantiate(headbox_prefab_, this.transform);
            originalParent = parent;
            parent.transform.localScale = this.size_;

            if (!string.IsNullOrEmpty(prefab_path_))
            {

                GameObject newParent = parent.transform.Find(prefab_path_).gameObject;
                if(newParent == null)
                {
                    Debug.Log("Path was invalid!");
                }
                else
                {
                    parent = newParent;
                }
            }
            seuratMesh = Instantiate(current_obj_);
        }
        else
        {
            parent = gameObject;
            seuratMesh = Instantiate(current_obj_);
            originalParent = seuratMesh;
        }
        seuratMesh.GetComponentInChildren<Renderer>().material = newMat;
        // Ensure seurat mesh is not scaled
        seuratMesh.transform.parent = parent.transform;
        seuratMesh.transform.localPosition = Vector3.zero;
        seuratMesh.transform.localRotation = Quaternion.AngleAxis(180, Vector3.up);
        seuratMesh.SetActive(setActiveAfter);

        if (removeCaptureAfter)
        {
            originalParent.name = this.name;
            originalParent.transform.parent = this.transform.parent;
            DestroyImmediate(this.gameObject);
        }

    }

    void OnDrawGizmos()
    {
        // The headbox is defined in camera coordinates.
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(Vector3.zero, size_);
    }
}


