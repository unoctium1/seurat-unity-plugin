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
using UnityEditor;
using System.IO;
using Debug = UnityEngine.Debug;

// Reflects status updates back to CaptureWindow, and allows CaptureWindow to
// notify capture/baking tasks to cancel.
namespace Seurat
{
	class EditorBakeStatus : CaptureStatus
	{
		bool task_cancelled_ = false;
		CaptureWindow bake_gui_;

		public override void SendProgress(string message, float fraction_complete)
		{
			bake_gui_.SetProgressBar(message, fraction_complete);
		}

		public override bool TaskContinuing()
		{
			return !task_cancelled_;
		}

		public void SetGUI(CaptureWindow bake_gui) { bake_gui_ = bake_gui; }

		public void CancelTask()
		{
			Debug.Log("User canceled capture processing.");
			task_cancelled_ = true;
		}
	}

	class SeuratRunnerStatus : PipelineStatus
	{
		SeuratWindow runner_gui_;
		bool task_cancelled_ = false;
		char[] seperators = { '[', ']' };

		public override bool TaskContinuing()
		{
			return !task_cancelled_;
		}

		public void CancelTask()
		{
			Debug.Log("User canceled seurat processing.");
			task_cancelled_ = true;
		}

		public override void SendErrorMessage(string message)
		{
			Debug.LogError(message);
		}

		public override void SendMessage(string message)
		{
			Debug.Log(message);
		}

		public override void SetProgressBar(string message)
		{
			string[] parts = message.Split(seperators,3);
			float value = ((float)parts[1].LastIndexOf('+')) / ((float)parts[1].Length);
			runner_gui_.SetProgressBar(parts[0] + parts[2], value);
		}
		public override void SendInfoMessage(string message)
		{
			runner_gui_.SetProgressBar(message.Substring(message.IndexOf(':')), 0.0f);
			Debug.LogWarning(message);
		}

		public void SetGUI(SeuratWindow gui)
		{
			runner_gui_ = gui;
		}
	}

	// Provides an interactive modeless GUI during the capture and bake process.
	class CaptureWindow : EditorWindow
	{
		// Defines a state machine flow for the capture and bake process.
		enum BakeStage
		{
			kInitialization,
			kCapture,
			// This stage indicates the GUI is waiting for user to dismiss the window
			// by pressing a "Done" button.
			kWaitForDoneButton,
			kComplete,
		}

		const float kTimerInterval = 0.016f;
		const int kTimerExpirationsPerCapture = 4;

		float last_time_;
		float ui_timer_ = 0.25f;
		int capture_timer_;

		string progress_message_;
		float progress_complete_;
		// The headbox component receives notification that capture is complete to
		// update the Inspector GUI, e.g. unlock the Capture button.
		CaptureHeadbox capture_notification_component_;
		CaptureBuilder monitored_capture_;
		EditorBakeStatus capture_status_;

		BakeStage bake_stage_ = BakeStage.kInitialization;

		public void SetupStatus(EditorBakeStatus capture_status)
		{
			capture_status_ = capture_status;
			capture_status_.SetGUI(this);
		}

		public void SetupCaptureProcess(CaptureHeadbox capture_notification_component,
		  CaptureBuilder capture)
		{
			capture_timer_ = kTimerExpirationsPerCapture;
			bake_stage_ = BakeStage.kCapture;
			last_time_ = Time.realtimeSinceStartup;
			capture_notification_component_ = capture_notification_component;
			monitored_capture_ = capture;
		}

		public void SetProgressBar(string message, float fraction_complete)
		{
			progress_message_ = message;
			progress_complete_ = fraction_complete;
		}

		public void OnGUI()
		{
			// Reserve layout space for the progress bar, equal to the space for a
			// textfield:
			Rect progress_rect = GUILayoutUtility.GetRect(18, 18, "TextField");
			EditorGUI.ProgressBar(progress_rect, progress_complete_, progress_message_);
			EditorGUILayout.Space();

			if (bake_stage_ != BakeStage.kWaitForDoneButton)
			{
				if (GUILayout.Button("Cancel"))
				{
					if (capture_status_ != null)
					{
						capture_status_.CancelTask();
					}
				}
			}

			if (bake_stage_ == BakeStage.kWaitForDoneButton)
			{
				if (GUILayout.Button("Done"))
				{
					bake_stage_ = BakeStage.kComplete;
				}
			}
		}

		private bool UpdateAndCheckUiTimerReady()
		{
			bool ui_timer_ready = false;
			float delta_time = Time.realtimeSinceStartup - last_time_;
			last_time_ = Time.realtimeSinceStartup;
			ui_timer_ -= delta_time;
			if (ui_timer_ <= 0.0f)
			{
				ui_timer_ready = true;
				// Prevent the timer from going infinitely negative due to large real time
				// intervals, e.g. from slow frame capture rendering.
				if (ui_timer_ <= -kTimerInterval)
				{
					ui_timer_ = 0.0f;
				}
				ui_timer_ += kTimerInterval;
			}
			return ui_timer_ready;
		}

		public void Update()
		{
			if (capture_status_ != null && capture_status_.TaskContinuing() && !UpdateAndCheckUiTimerReady())
			{
				return;
			}

            // Refresh the Editor GUI to finish the task.
            UnityEditor.EditorUtility.SetDirty(capture_notification_component_);

			if (bake_stage_ == BakeStage.kCapture)
			{
				--capture_timer_;
				if (capture_timer_ == 0)
				{
					capture_timer_ = kTimerExpirationsPerCapture;

					monitored_capture_.RunCapture();

					if (monitored_capture_.IsCaptureComplete() &&
					  capture_status_.TaskContinuing())
					{
						monitored_capture_.EndCapture();
						monitored_capture_ = null;

						bake_stage_ = BakeStage.kWaitForDoneButton;
					}
				}

				if (capture_status_ != null && !capture_status_.TaskContinuing())
				{
					bake_stage_ = BakeStage.kComplete;
					if (monitored_capture_ != null)
					{
						monitored_capture_.EndCapture();
						monitored_capture_ = null;
					}
				}
			}

			// Repaint with updated progress the GUI on each wall-clock time tick.
			Repaint();
		}

		public bool IsComplete()
		{
			return bake_stage_ == BakeStage.kComplete;
		}
	};

	// Provides an interactive modeless GUI to monitor the seurat executable.
	class SeuratWindow : EditorWindow
	{
		// Defines a state machine flow for the capture and bake process.
		enum BakeStage
		{
			kInitialization,
			kRunning,
			// This stage indicates the GUI is waiting for user to dismiss the window
			// by pressing a "Done" button.
			kWaitForDoneButton,
			kComplete,
		}

		string progress_message_;
		float progress_complete_;
		// The headbox component receives notification that capture is complete to
		// update the Inspector GUI, e.g. unlock the Capture button.
		CaptureHeadbox capture_notification_component_;
		SeuratPipelineRunner monitored_runner_;
		SeuratRunnerStatus runner_status_;

		BakeStage bake_stage_ = BakeStage.kInitialization;

		public void SetupStatus(SeuratRunnerStatus runner_status)
		{
			runner_status_ = runner_status;
			runner_status_.SetGUI(this);
		}

		public void SetupRunnerProcess(CaptureHeadbox capture_notification_component,
		  SeuratPipelineRunner runner)
		{
			bake_stage_ = BakeStage.kRunning;
			capture_notification_component_ = capture_notification_component;
			monitored_runner_ = runner;
			monitored_runner_.Run();
		}

		public void SetProgressBar(string message, float fraction_complete)
		{
			progress_message_ = message;
			progress_complete_ = fraction_complete;
		}

		public void OnGUI()
		{
			// Reserve layout space for the progress bar, equal to the space for a
			// textfield:
			Rect progress_rect = GUILayoutUtility.GetRect(18, 18, "TextField");
			EditorGUI.ProgressBar(progress_rect, progress_complete_, progress_message_);
			EditorGUILayout.Space();

			if (bake_stage_ != BakeStage.kWaitForDoneButton)
			{
				if (GUILayout.Button("Cancel"))
				{
					if (runner_status_ != null)
					{
						runner_status_.CancelTask();
					}
				}
			}

			if (bake_stage_ == BakeStage.kWaitForDoneButton)
			{
				if (GUILayout.Button("Done"))
				{
					bake_stage_ = BakeStage.kComplete;
				}
			}
		}

		public void Update()
		{

			// Refresh the Editor GUI to finish the task.
			UnityEditor.EditorUtility.SetDirty(capture_notification_component_);

			if (bake_stage_ == BakeStage.kRunning)
			{

				if(!monitored_runner_.IsProcessRunning() && runner_status_.TaskContinuing())
				{
					bake_stage_ = BakeStage.kWaitForDoneButton;
				}


				if (runner_status_ != null && !runner_status_.TaskContinuing())
				{
					bake_stage_ = BakeStage.kComplete;
					if (monitored_runner_ != null)
					{
						monitored_runner_.InterruptProcess();
						monitored_runner_ = null;
					}
				}
			}

			// Repaint with updated progress the GUI on each wall-clock time tick.
			Repaint();
		}

		public bool IsComplete()
		{
			return bake_stage_ == BakeStage.kComplete;
		}
	};

	// Implements the Capture Headbox component Editor panel.
	[CustomEditor(typeof(CaptureHeadbox))]
	public class CaptureHeadboxEditor : Editor
	{
		public static readonly string kSeuratCaptureDir = "SeuratCapture";

		SerializedProperty output_folder_;
		SerializedProperty seurat_output_folder_;
		SerializedProperty output_name_;
		SerializedProperty run_exec_;
		SerializedProperty size_;
		SerializedProperty samples_;
		SerializedProperty center_resolution_;
		SerializedProperty resolution_;
		SerializedProperty dynamic_range_;
		SerializedProperty last_output_dir_;
		SerializedProperty options_;
		SerializedProperty use_cache_;
		SerializedProperty cache_folder_;
		SerializedProperty asset_path_;
		SerializedProperty obj_;
		SerializedProperty tex_;
		SerializedProperty headbox_prefab_;
		SerializedProperty seurat_shader_;
		SerializedProperty prefab_path_;
		SerializedProperty render_queue_;
		SerializedProperty use_mat_;
		SerializedProperty material_path_;
		SerializedProperty mat_;

		EditorBakeStatus capture_status_;
		CaptureWindow bake_progress_window_;
		CaptureBuilder capture_builder_;
		SeuratPipelineRunner pipeline_runner_;
		SeuratRunnerStatus status_interface_;
		SeuratWindow runner_progress_window_;

		bool draw_capture_;
		bool draw_pipeline_;
		bool draw_import_;
		bool draw_scenebuilder_;

		void OnEnable()
		{
			output_folder_ = serializedObject.FindProperty("output_folder_");
			run_exec_ = serializedObject.FindProperty("seurat_exec_");
			seurat_output_folder_ = serializedObject.FindProperty("seurat_output_folder_");
			output_name_ = serializedObject.FindProperty("seurat_output_name_");
			size_ = serializedObject.FindProperty("size_");
			samples_ = serializedObject.FindProperty("samples_per_face_");
			center_resolution_ = serializedObject.FindProperty("center_resolution_");
			resolution_ = serializedObject.FindProperty("resolution_");
			dynamic_range_ = serializedObject.FindProperty("dynamic_range_");
			last_output_dir_ = serializedObject.FindProperty("last_output_dir_");
			options_ = serializedObject.FindProperty("options");
			use_cache_ = serializedObject.FindProperty("use_cache_");
			cache_folder_ = serializedObject.FindProperty("cache_folder_");
			asset_path_ = serializedObject.FindProperty("asset_path_");
			obj_ = serializedObject.FindProperty("current_obj_");
			tex_ = serializedObject.FindProperty("current_tex_");
			headbox_prefab_ = serializedObject.FindProperty("headbox_prefab_");
			seurat_shader_ = serializedObject.FindProperty("seurat_shader_");
			prefab_path_ = serializedObject.FindProperty("prefab_path_");
			render_queue_ = serializedObject.FindProperty("render_queue_");
			use_mat_ = serializedObject.FindProperty("use_mat_");
			material_path_ = serializedObject.FindProperty("material_path_");
			mat_ = serializedObject.FindProperty("mat_");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			draw_capture_ = EditorUtility.DrawMethodGroup(draw_capture_, "Capture Settings", DrawCaptureSettings);
			draw_pipeline_ = EditorUtility.DrawMethodGroup(draw_pipeline_, "Pipeline Settings", DrawPipelineSettings);
			draw_import_ = EditorUtility.DrawMethodGroup(draw_import_, "Import Settings", DrawImportSettings);
			draw_scenebuilder_ = EditorUtility.DrawMethodGroup(draw_scenebuilder_, "Scene Builder Settings", DrawSceneBuilderSettings);

			DrawButtons();

			serializedObject.ApplyModifiedProperties();

			// Poll the bake status.
			if (bake_progress_window_ != null && bake_progress_window_.IsComplete())
			{
				bake_progress_window_.Close();
				bake_progress_window_ = null;
				capture_builder_ = null;
				capture_status_ = null;
			}
			if (runner_progress_window_ != null && runner_progress_window_.IsComplete())
			{
				runner_progress_window_.Close();
				runner_progress_window_ = null;
				pipeline_runner_ = null;
				status_interface_ = null;
			}

		}

        #region DRAW_GUI_HELPERS
        private void DrawCaptureSettings()
		{
			EditorGUILayout.PropertyField(output_folder_, new GUIContent(
		  "Output Folder"));
			if (GUILayout.Button("Choose Output Folder"))
			{
				string path = UnityEditor.EditorUtility.SaveFolderPanel(
				  "Choose Capture Output Folder", output_folder_.stringValue, "");
				if (path.Length != 0)
				{
					output_folder_.stringValue = path;
				}
			}

			EditorGUILayout.PropertyField(size_, new GUIContent("Headbox Size"));
			EditorGUILayout.PropertyField(samples_, new GUIContent("Sample Count"));
			EditorGUILayout.PropertyField(center_resolution_, new GUIContent(
			  "Center Capture Resolution"));
			EditorGUILayout.PropertyField(resolution_, new GUIContent(
			  "Default Resolution"));
			EditorGUILayout.PropertyField(dynamic_range_, new GUIContent(
			  "Dynamic Range"));

			EditorGUILayout.PropertyField(last_output_dir_, new GUIContent(
			  "Last Output Folder"));
		}

		private void DrawPipelineSettings()
		{
			EditorGUILayout.PropertyField(run_exec_, new GUIContent(
			  "Seurat Executable"));
			if (GUILayout.Button("Choose Excutable Location"))
			{
				string path = UnityEditor.EditorUtility.OpenFilePanel(
				  "Choose Seurat Executable Location", Application.dataPath, "exe");
				if (path.Length != 0)
				{
					run_exec_.stringValue = path;
				}
			}

			EditorGUILayout.PropertyField(seurat_output_folder_, new GUIContent(
			  "Seurat Output Folder"));
			if (GUILayout.Button("Choose Output Folder for Seurat Pipeline"))
			{
				string path = UnityEditor.EditorUtility.SaveFolderPanel(
				  "Choose Seurat Output Folder", seurat_output_folder_.stringValue, "");
				if (path.Length != 0)
				{
					seurat_output_folder_.stringValue = path;
				}
			}
			EditorGUILayout.PropertyField(output_name_, new GUIContent(
		  "Seurat Pipeline Output Name"));

			EditorGUILayout.PropertyField(use_cache_, new GUIContent("Use Geometry Cache"));
			if (use_cache_.boolValue)
			{
				EditorGUILayout.PropertyField(cache_folder_, new GUIContent(
			  "Cache Folder"));
				if (GUILayout.Button("Choose Folder for Geometry cache"))
				{
					string path = UnityEditor.EditorUtility.SaveFolderPanel(
					  "Choose Cache Folder", cache_folder_.stringValue, "");
					if (path.Length != 0)
					{
						cache_folder_.stringValue = path;
					}
				}
			}

			EditorGUILayout.PropertyField(options_, new GUIContent(
		  "Commandline Options"), true);
		}

		private void DrawImportSettings()
		{
			EditorGUILayout.PropertyField(asset_path_, new GUIContent(
			  "Folder for Import"));
			if (GUILayout.Button("Choose Folder to Import Model & Tex to"))
			{
				string path = UnityEditor.EditorUtility.SaveFolderPanel(
				  "Choose Import Location", Application.dataPath, "");
				if (path.Length != 0)
				{
					if (path.StartsWith(Application.dataPath))
					{
						asset_path_.stringValue = path.Substring(Application.dataPath.Length);
					}
					else
					{
						Debug.LogError("Path must be in assets folder");
					}
				}
			}
			EditorGUILayout.PropertyField(obj_, new GUIContent(
				"Imported Model"));
			EditorGUILayout.PropertyField(tex_, new GUIContent(
				"Imported Texture"));
		}

		private void DrawSceneBuilderSettings()
		{
			EditorGUILayout.PropertyField(headbox_prefab_, new GUIContent(
				"Headbox Prefab"));
			EditorGUILayout.PropertyField(seurat_shader_, new GUIContent(
				"Material Shader"));
			EditorGUILayout.PropertyField(prefab_path_, new GUIContent(
				"Relative Path"));

			EditorGUILayout.PropertyField(render_queue_, new GUIContent(
				"Render Queue"));
			EditorGUILayout.PropertyField(use_mat_, new GUIContent(
				"Save material?"));
			if (use_mat_.boolValue)
			{
				EditorGUILayout.PropertyField(material_path_, new GUIContent(
			  "Folder for Materials"));
				if (GUILayout.Button("Choose Folder to Save Material to"))
				{
					string path = UnityEditor.EditorUtility.SaveFolderPanel(
					  "Choose Material Location", Application.dataPath, "");
					if (path.Length != 0)
					{
						if (path.StartsWith(Application.dataPath))
						{
							material_path_.stringValue = "Assets" + path.Substring(Application.dataPath.Length);
						}
						else
						{
							Debug.LogError("Path must be in assets folder");
						}
					}
				}

				if (GUILayout.Button("Set up material"))
				{
					BuildMaterial();
				}

				EditorGUILayout.PropertyField(mat_, new GUIContent(
					"Current material"));
			}

		}

		private void DrawButtons()
		{
			if (capture_status_ != null)
			{
				GUI.enabled = false;
			}
			if (GUILayout.Button("Capture"))
			{
				Capture();
			}
			GUI.enabled = true;
			if (status_interface_ != null || run_exec_.stringValue.Length == 0)
			{
				GUI.enabled = false;
			}
			if (GUILayout.Button("Run Seurat"))
			{
				RunSeurat();
			}
			GUI.enabled = true;
			if (GUILayout.Button("Import seurat outputs"))
			{
				ImportAssets();
			}
			if (GUILayout.Button("Build Seurat Capture in Scene"))
			{
				BuildScene();
			}
		}

        #endregion //DRAW GUI HELPERS

        #region BUTTON_COMMANDS

        public void Capture()
		{
			CaptureHeadbox headbox = (CaptureHeadbox)target;

			string capture_output_folder = headbox.output_folder_;
			if (capture_output_folder.Length <= 0)
			{
				capture_output_folder = FileUtil.GetUniqueTempPathInProject();
			}
			headbox.last_output_dir_ = capture_output_folder;
			Directory.CreateDirectory(capture_output_folder);

			capture_status_ = new EditorBakeStatus();
			capture_builder_ = new CaptureBuilder();

			// Kick off the interactive Editor bake window.
			bake_progress_window_ = (CaptureWindow)EditorWindow.GetWindow(typeof(CaptureWindow));
			bake_progress_window_.SetupStatus(capture_status_);

			capture_builder_.BeginCapture(headbox, capture_output_folder, 1, capture_status_);
			bake_progress_window_.SetupCaptureProcess(headbox, capture_builder_);
		}

		public void RunSeurat()
		{
			CaptureHeadbox headbox = (CaptureHeadbox)target;
			string seurat_output_folder = headbox.seurat_output_folder_;
			if (seurat_output_folder.Length <= 0)
			{
				seurat_output_folder = FileUtil.GetUniqueTempPathInProject();
				headbox.seurat_output_folder_ = seurat_output_folder;
			}
			if (headbox.use_cache_)
			{
				string cache_folder_ = headbox.cache_folder_;
				if (cache_folder_.Length <= 0)
				{
					cache_folder_ = FileUtil.GetUniqueTempPathInProject();
				}
				Directory.CreateDirectory(cache_folder_);
			}
			Directory.CreateDirectory(seurat_output_folder);
			string args = headbox.GetArgString();

			status_interface_ = new SeuratRunnerStatus();
			pipeline_runner_ = new SeuratPipelineRunner(
				args,
				headbox.seurat_exec_,
				status_interface_);

			runner_progress_window_ = (SeuratWindow)EditorWindow.GetWindow(typeof(SeuratWindow));
			runner_progress_window_.SetupStatus(status_interface_);

			runner_progress_window_.SetupRunnerProcess(headbox, pipeline_runner_);
		}

		public void ImportAssets()
		{
			CaptureHeadbox headbox = (CaptureHeadbox)target;
			headbox.CopyFiles();
			headbox.ImportSeurat();
			headbox.FetchAssets();
            UnityEditor.EditorUtility.SetDirty(headbox);
		}

		public void BuildMaterial()
		{
			CaptureHeadbox headbox = (CaptureHeadbox)target;
			headbox.CreateMaterial();
		}

		public void BuildScene()
		{
			CaptureHeadbox headbox = (CaptureHeadbox)target;
			headbox.BuildCapture();
		}

        #endregion //BUTTON COMMANDS

    }
}
