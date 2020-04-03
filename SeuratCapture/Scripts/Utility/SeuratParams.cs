using System.IO;
using UnityEngine;

namespace Seurat
{
    [System.Serializable]
    public struct SeuratParams
    {
        [Tooltip("Determines whether output textures use premultiplied alpha. Unity expects true, Butterfly expects false")]
        public bool premultiply_alphas;
        [Tooltip("Gamma-correction exponent")]
        public float gamma;
        [Tooltip("The maximum number of triangles to generate")]
        public int triangle_count;
        [Tooltip("Half the side-length of the origin-centered skybox to clamp distant geometry. 0.0 indicates no skybox clamping should be performed")]
        public float skybox_radius;
        [Tooltip("Prefer speed over quality")]
        public bool fast_preview;
        public string GetArgs()
        {

            return " -gamma=" + gamma + " -premultiply_alpha=" + (premultiply_alphas ? "true" : "false") + " -triangle_count=" + triangle_count + " -skybox_radius=" + skybox_radius + " -fast_preview=" + (fast_preview ? "true" : "false");
        }
    }
}
