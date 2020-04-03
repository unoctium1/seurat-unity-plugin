using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Seurat
{
    [ExecuteInEditMode]
    public class SeuratAutomator : MonoBehaviour
    {
        // Capture Settings
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

        // Pipeline Settings
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

        // Import Settings
        [Tooltip("Folder to import meshes & textures to")]
        public string asset_path_;
        [Tooltip("List of all seurat meshes imported")]
        public GameObject[] cur_meshes_;
        [Tooltip("List of all seurat textures imported")]
        public Texture2D[] cur_tex_;

        // Scene Builder Settings
        [Tooltip("Prefab to be resized for the headbox")]
        public GameObject headbox_prefab_;
        [Tooltip("Shader to use for each seurat material")]
        public Shader seurat_shader_;
        [Tooltip("Relative path to place each seurat mesh in, relative to the headbox prefab. Leave blank to spawn at root")]
        public string prefab_path_;

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

        public void OverrideSceneBuilder(CaptureHeadbox head)
        {
            head.prefab_path_ = prefab_path_;
            head.headbox_prefab_ = headbox_prefab_;
            head.seurat_shader_ = seurat_shader_;
        }

        public void BuildScene()
        {
            GameObject scene = Instantiate(this.gameObject);
            CaptureHeadbox[] headboxes = scene.GetComponentsInChildren<CaptureHeadbox>();
            foreach (CaptureHeadbox box in headboxes)
            {
                box.BuildCapture(true, false);
            }
            SeuratAutomator auto = scene.GetComponent<SeuratAutomator>();
            DestroyImmediate(auto);
        }
    }
}
