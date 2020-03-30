using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class SeuratAutomator : MonoBehaviour
{
    [SerializeField, Tooltip("Folders for each headbox will be created in this folder")]
    public string output_folder_;
    [SerializeField, Tooltip("If true, all child capture headboxes will have their parameters overriden by the Override settings below")]
    private bool override_all_ = true;

    [Header("Override Settings")]

    [SerializeField, Tooltip("The number of samples per face of the headbox.")]
    private PositionSampleCount samples_per_face_ = PositionSampleCount.k32;
    [SerializeField, Tooltip("The resolution of the center image, taken at the camera position at the center of the headbox. This should be 4x higher than the resolution of the remaining samples, for antialiasing.")]
    private CubeFaceResolution center_resolution_ = CubeFaceResolution.k4096;
    [SerializeField, Tooltip("The resolution of all samples other than the center.")]
    private CubeFaceResolution resolution_ = CubeFaceResolution.k1024;
    [SerializeField, Tooltip("Capture in standard (SDR) or high dynamic range (HDR). HDR requires floating-point render targets, the Camera Component have allow HDR enabled, and enables EXR output.")]
    private CaptureDynamicRange dynamic_range_ = CaptureDynamicRange.kSDR;

    [Header("Pipeline Settings")]
    [SerializeField, Tooltip("Path to the executable that runs the seurat pipeline")]
    public string seurat_executable_path_;
    [Tooltip("If true, cache output geometry in cache folders. Cache speeds up repeated iterations, useful if iterating on textures. Unique folder will be created for each capture.")]
    public bool use_cache_;
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

    [Header("Import Settings")]
    [Tooltip("Folder to import meshes & textures to")]
    public string asset_path_;

    public void OverrideHeadbox(CaptureHeadbox head)
    {
        if (override_all_)
        {
            head.samples_per_face_ = samples_per_face_;
            head.center_resolution_ = center_resolution_;
            head.resolution_ = resolution_;
            head.dynamic_range_ = dynamic_range_;
        }
    }

    public void OverrideParams(CaptureHeadbox head)
    {
        head.options = options;
    }
}
