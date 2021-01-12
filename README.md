<<<<<<< HEAD
# Unity Seurat Plugin

### Note: This is a fork of a deprecated Google project. The original readme continues from the section 'Importing Seurat Meshes Into Unity'
The original readme goes over the manual import process, as well as how to diagnose issues with the resulting mesh

Seurat is a scene simplification technology designed to process very complex 3D scenes into a representation that renders efficiently on mobile 6DoF VR systems. This plugin aims to expedite the Seurat creation process.

The process consists of 4 stages: 
- Capturing images to generate seurat meshes
- Running the seurat pipeline to generate the meshes
- Importing the meshes into Unity
- Setting up a scene with imported meshes

This plugin allows the user to perform all of these from within Unity. In addition, for large or nonconvex spaces, multiple captures can be done at once. 

The plugin consists of two monobehaviour scripts for use, [SeuratAutomator.cs](/SeuratCapture/Scripts/SeuratAutomator.cs) and [CaptureHeadbox.cs](/SeuratCapture/Scripts/CaptureHeadbox.cs). CaptureHeadbox allows capturing and building a single mesh, while SeuratAutomator will automate the process for all children with a CaptureHeadbox component. 

## Requirements
- Unity - Tested against Unity 2018.4
- Seurat Pipeline Executable - See [Seurat repo](https://github.com/googlevr/seurat) or download precompiled binary [here](https://github.com/ddiakopoulos/seurat/releases)

## Usage
1. In Unity, setup scene to capture. All elements of the scene should be set up (lighting, skybox, etc).

2. Depending on how large the walkable area in the scene is, and whether it is convex or not, use either a single CaptureHeadbox or a collection of Headboxes and a SeuratAutomator. 

### Capturing Images
3. For each headbox in the scene, set the size of the headbox. Size should specify some convex walkable area within the scene. Height of the headbox will usually be 3.
4. Set capture settings in the inspector. Capture settings control how many images should be captures, as well as their resolution and quality. If you are using multiple headboxes, and all headboxes have the same settings, check the 'Override All' box in the Automator inspector, and set settings there. Otherwise, set settings in the Headbox inspector.
5. Choose an output folder. For an individual headbox, captures will be created in the folder. For an automator rig, folders will be created for each headbox.
6. Click the capture button to capture all headboxes.

### Running the Pipeline
7. In the inspector, set the path to the downloaded or build seurat executable
8. If you're using a single headbox, select both the output folder and put in an output name. If using the automator, these will be generated. 
9. Select the 'use cache' option and choose a cache path if you intend to iterate upon textures. This stores geometry in a cache and drastically speeds up repeated iterations. 
10. If you wish to override or choose any custom commandline parameters, specify them. Descriptions of parameters can be found [here](https://github.com/googlevr/seurat#command-line-parameters). Of particular note is the 'fast preview' option, which speeds up performance at the cost of quality - useful for previewing headboxes. 
11. Click the Run Seurat button to run the seurat tool. Note that it takes a while to run - if for some reason it must be stopped partway through, hit the 'stop seurat' button.

### Importing Meshes
12. Select a path in the assets folder to import the mesh and texture too. 
13. Hit 'Import Seurat.' Note that this process also takes a while, and is thread blocking - Unity WILL freeze until its finished.
  - After importing, you can check the process was completed successfully if the object and texture fields are not null. 
  - Alternatively, if these steps aren't working, follow the below steps to manually import, and then drag in the imported assets

### Building the Seurat Mesh in the Scene
14. Select the Seurat shader to use - this should be either GoogleVR/AlphaBlended or GoogleVR/AlphaTested
15. Select the headbox prefab. This will be some object that the mesh will be childed to, that will be resized to match the size specified in step 3. 
16. Input the relative path where the mesh should be spawned. For example, if the headbox prefab is "obj1" and it has a child "obj2", which has a nested child "obj3" where the mesh should be spawned, input "obj2/obj3". Leave blank to spawn at the headbox prefab. 
 - The "SampleFramework" folder contains sample prefabs and scripts for headbox behavior. 
17. Select the render queue. This should be around 1999, so that other objects render on top of it.
18. If you wish to create materials, check the "use materials" box. Building a scene will work without this, but temporary materials will not survive common Unity operations like changing the scene or making a prefab out of the finished meshes. 
  - If using materials, select a folder to put materials in, and then click 'Build Materials'
  - When finished, there will be a reference to the material/materials in the inspector.
19. Click build scene. If using the CaptureHeadbox, the mesh will be built as a child of the headbox. If using the automator, a copy of the automator will be made with all meshes as children of it. Building with the automator will remove all headbox and automator components from the finished mesh. Building with CaptureHeadbox will not remove anything.
=======
# Tutorial 
Tutorial: https://connect.unity.com/p/google-s-seurat-rendering-hdrp

# Compiled Seurat 
Compiled Seurat Repo: https://github.com/ddiakopoulos/seura...
>>>>>>> hdrp

# Importing Seurat Meshes Into Unity

Seurat is a scene simplification technology designed to process very complex 3D scenes into a representation that renders efficiently on mobile 6DoF VR systems.

This document covers how to import Seurat meshes into Unity. To learn more about the Seurat pipeline, visit the main [Seurat GitHub page](https://github.com/googlevr/seurat).

## Introduction

This document is organized into two sections. The first describes the steps to
load a mesh produced by Seurat into Unity. The second provides detailed
diagnostic steps to examine if the imported Seurat mesh shows artifacts, gaps,
or cracks in various places, typically along the edges of the mesh.

The document assumes some familiarity with the Unity Editor, and is written
against version 5.6.*.

## Importing Seurat Meshes

The instructions in this section assume the following file layout:
`c:\Unity_Projects\SeuratImport` contains a blank Unity project.
`c:\Seurat_Output` contains a set of files produced by Seurat: in particular
`seurat.obj`, `seurat.png`.  Follow these steps to import the Seurat output into
Unity:

1. Import Prerequisites
   * Open the SeuratImport project in Unity.
   * Import the Seurat Unity capture package into the project with
     _Assets | Import Package | Custom Package_.
2. Import the Seurat mesh and texture as an Asset
   * Use _Asset | Import New Asset_ to copy seurat.obj and seurat.png into the
     Unity project’s Assets folder.
   * Browse the Assets folder in Project window.
   * Locate the Seurat output model `seurat.obj` in the Assets folder.
3. Add the Seurat mesh to the Scene.
   * Drag and drop the `seurat.obj` model from the Asset folder into the Scene
     window (or Hierarchy window, as appropriate).

     Note: Unity may split the mesh into several parts to fit under vertex count
     limits.
   * Unity should then display a solid-shaded version of the Seurat mesh.
4. Apply the Seurat shader to the Seurat mesh.
   * Locate the new node, _seurat_ instancing the seurat.obj in the Hierarchy
     window, and expand the hierarchy it contains until the leaf nodes are
     visible. The hierarchy should contain something like the following nodes,
     and the leaf nodes will have _Mesh Render_ components attached:
     * seurat
       * default
         * default_MeshPart0
         * default_MeshPart1
         * default_MeshPart2
    * Select the first leaf node a _Mesh Render_ component, _default_MeshPart0_.
    * Locate the _Mesh Render_ component in the Inspector panel.
    * Apply the Seurat shader to the geometry; click the Shaders popup at the
      bottom of the panel, and navigate the menu to the shader GoogleVR |
      Softserve | AlphaBlended, and click that menu option to apply the alpha
      blended material.
5. Apply the Seurat texture atlas to the mesh
   * Locate the Seurat output texture atlas seurat.png in the Assets folder.
   * Apply the texture atlas to the chunks of the Seurat mesh: drag and drop
     seurat.png onto each of the leaf nodes, here named _default_MeshPart*_.
6. Configure Texture Atlas Settings
   * Select the seurat.png texture in the Assets browser.
   * Locate the Inspector panel for the texture.
   * Expand the _Advanced_ rollup.
   * Disable the option _Generate Mip Maps_.
   * Change _Wrap Mode_ to _Clamp_.
   * Locate the build platform subpanel.
   * Enable _Override for PC, Mac, & Linux Standalone_.
   * Change _Max Size_ to a resolution greater-than or equal-to the dimensions
     of the seurat.png. Typically this will be 4096, but depends on Seurat
     processing settings. Note: Seurat requires that Unity not resize the
     texture!
   * Click the _Apply_ button at the bottom of the panel.
   * Unity will reprocess the texture, and should now display the Seurat mesh
     correctly.

If the Seurat output has artifacts, or does not look correct, please continue on
to the next section. The section provides detailed instructions on configuring
both the imported assets, Unity project settings to correctly render Seurat
meshes.

## Diagnosing Cracks
This section illustrates what crack artifacts may appear, and lists many Unity
settings that can trigger these artifacts.
![Example of cracks in Unity](images/cracks_01.png)
![Example of cracks in Unity](images/cracks_02.png)

### Determine the cause
The easiest way to determine the cause of crack or gap artifacts in Seurat
output is to set the camera background color to something with great contrast to
the scene (e.g. bright red) and see if there are holes in the mesh generated by
Seurat.

* If you see holes in the mesh, you should try to rebake with higher quality
  settings.
* If you do not see holes, adjust texture and shader settings.

### Texture Settings
* Bilinear Filtering
* For premultiplied alpha, uncheck _Alpha is Transparency_. Otherwise, Unity
  will inpaint the transparent areas of the texture (this process can be
  lengthy) and will show artifacts in areas that are supposed to be completely
  transparent.
* NO mip maps
* Low or No anisotropic filtering ~ 1-2 in Unity, any higher may cause cracks
* Do not autoresize to power of 2
* Wrap mode: clamp
* A Unity project setting can affect the texture resolution during the Unity
  application build. Check that the _Texture Quality_ option under _Edit |
  Project Settings | Quality_ is set to _Full Res_.

### Mesh Settings
* Make sure mesh compression is turned off for the UV0 channel in _Project
  Settings | Player | Android | Vertex Compression_

### Shader Settings

#### Centroid and Anti Aliasing
If you are using MSAA, you may notice edge artifacts.  Centroid interpolation
will fix edge sampling errors caused by MSAA.  For more information, see Fabien
Giesen’s post.  In Unity, this can be done by appending `_centroid` to the
`TEXCOORD#` interpolator semantic like so:

```glsl
struct VertexToFragment {
  float4 position : SV_POSITION;
  float2 uv : TEXCOORD0_centroid;
}
```

Fragment shader texture coordinate precision is important. Use `highp` or
`float` precision for texture coordinate variables rather than `lowp` modifier
or the HLSL `min16` prefix.

IMPORTANT: Centroid requires Open GL ES 3.0, and is performance intensive.  Only
use centroid interpolation if you are using MSAA, and absolutely need it.
Currently the _centroid modifier is implicated in GPU driver issues on Pixel
devices. Workarounds / bug fixes are in progress.

Unless you absolutely need depth write (e.g. you are doing something fancy, like
casting dynamic shadows off Seurat geometry) - you should prefer Alpha Blending.

#### Alpha Blended
* UV0 set to _centroid interpolation OR disable MSAA
* Cull Off
* ZWrite Off
* ZTest LEqual
* Queue: Transparent
* Blend SrcAlpha OneMinusSrcAlpha

#### Alpha Tested
* UV0 set to _centroid interpolation OR disable MSAA
* Cull Off
* ZWrite On
* ZTest LEqual
* Queue: Transparent
* Blend SrcAlpha OneMinusSrcAlpha
* Alpha-to-coverage
* Unity: AlphaToMask On

### Skybox, Clear Color and Background
Some Seurat scenes can have gaps (cracks, you could say) of varying size against
the background. You should let the team know if you encounter these. Still,
colors from background color can bleed through and appear as cracks.

Several things in Unity can generate a background color:

1. Geometry in the scene drawn before Seurat’s mesh. Try toggling it on and off
   to see if a skybox mesh is generating cracks, for example.
2. The _Skybox Material_ option of the Scene tab of Lighting inspector panel
   (_Window | Lighting | Settings_), can control the background color. To
   evaluate if this feature is contributing to the problem, try selecting a
   black material or a bright red material to see if this changes any of the
   cracks.
3. In the Camera inspector panel of the node containing the LDI Headbox for the
   capture, _Clear Flags_ and _Background Color_ control buffer color
   initialization for the capture.

### Capture Settings
If none of the above fixes the issue, or you see holes in the mesh - try
rebaking with higher quality capture settings.

DISCLAIMER: This is not an officially supported Google product.

